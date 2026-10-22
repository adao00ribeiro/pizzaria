import { DOCUMENT } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  HostListener,
  OnDestroy,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { MatBadgeModule } from '@angular/material/badge';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import {
  EMPTY,
  Subject,
  Subscription,
  catchError,
  filter,
  finalize,
  fromEvent,
  interval,
  merge,
  startWith,
  switchMap,
} from 'rxjs';

import { OrderType, Unit } from '../../core/models/common.models';
import {
  KitchenCounts,
  KitchenFlavor,
  KitchenItem,
  KitchenQueueItem,
  KitchenQueueResponse,
} from '../../core/models/kitchen.models';
import { ORDER_TYPE_LABELS, UNIT_LABELS } from '../../core/models/labels';
import { CancelOrderDialog } from './cancel-order.dialog';
import { KitchenSoundService } from './kitchen-sound.service';
import { KitchenService } from './kitchen.service';

/** Intervalo de polling da fila de produção (premissa P5: 8 segundos). */
const POLL_INTERVAL_MS = 8_000;

export type TimerSeverity = 'green' | 'yellow' | 'red';

/**
 * Painel da produção do pizzaiolo (docs/10, Sprint 3 — rota `/producao`).
 * Layout Kanban em 3 colunas (Recebido → Em preparo → Pronto), polling de 8s
 * pausado quando a aba está oculta, receita resumida expansível e aviso sonoro (P10).
 */
@Component({
  selector: 'pz-kitchen-page',
  standalone: true,
  imports: [
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatChipsModule,
    MatBadgeModule,
    MatDialogModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
    MatTooltipModule,
  ],
  templateUrl: './kitchen.page.html',
  styleUrl: './kitchen.page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KitchenPage implements OnInit, OnDestroy {
  private readonly service = inject(KitchenService);
  private readonly dialog = inject(MatDialog);
  private readonly snack = inject(MatSnackBar);
  private readonly doc = inject(DOCUMENT);

  readonly sound = inject(KitchenSoundService);

  readonly initialLoading = signal(true);
  readonly syncing = signal(false);
  readonly busyOrderId = signal<string | null>(null);
  readonly tabHidden = signal(false);

  readonly items = signal<KitchenQueueItem[]>([]);
  readonly counts = signal<KitchenCounts>({
    received: 0,
    inPreparation: 0,
    ready: 0,
    total: 0,
  });

  readonly lastSyncedAt = signal<number | null>(null);
  readonly nowTick = signal<number>(Date.now());

  /** IDs dos cards com a receita resumida expandida (preservado entre ciclos do polling). */
  readonly expandedIds = signal<ReadonlySet<string>>(new Set());

  readonly receivedOrders = computed(() =>
    this.items().filter((o) => o.status === 'Received'),
  );
  readonly inPreparationOrders = computed(() =>
    this.items().filter((o) => o.status === 'InPreparation'),
  );
  readonly readyOrders = computed(() =>
    this.items().filter((o) => o.status === 'Ready'),
  );

  /** Rótulo vivo "Atualizado há Xs" exibido no topo do painel. */
  readonly updatedAgoLabel = computed(() => {
    if (this.tabHidden()) {
      return 'Polling pausado (aba oculta)';
    }
    const last = this.lastSyncedAt();
    if (!last) {
      return 'Sincronizando…';
    }
    const diffSec = Math.max(0, Math.floor((this.nowTick() - last) / 1000));
    return `Atualizado há ${diffSec}s`;
  });

  private readonly manualRefresh$ = new Subject<void>();
  private readonly subscriptions = new Subscription();
  private knownReceivedIds = new Set<string>();
  private hasLoadedOnce = false;

  /** Destrava o Web Audio API no primeiro toque/clique/tecla do usuário (premissa P10). */
  @HostListener('document:pointerdown')
  @HostListener('document:keydown')
  onFirstUserGesture(): void {
    this.sound.unlock();
  }

  ngOnInit(): void {
    this.tabHidden.set(this.doc.hidden);

    // Atualiza o estado de visibilidade da aba e dispara refresh imediato ao voltar
    const visibilitySub = fromEvent(this.doc, 'visibilitychange').subscribe(() => {
      const hidden = this.doc.hidden;
      this.tabHidden.set(hidden);
      if (!hidden) {
        this.manualRefresh$.next();
      }
    });
    this.subscriptions.add(visibilitySub);

    // Relógio de 1s para atualizar o contador "Atualizado há Xs"
    const clockSub = interval(1000).subscribe(() => {
      this.nowTick.set(Date.now());
    });
    this.subscriptions.add(clockSub);

    // Polling de 8s com interval + switchMap, pausado quando a aba está oculta
    const pollSub = merge(
      interval(POLL_INTERVAL_MS).pipe(filter(() => !this.doc.hidden)),
      this.manualRefresh$,
    )
      .pipe(
        startWith(0),
        switchMap(() => {
          this.syncing.set(true);
          return this.service.queue().pipe(
            catchError(() => EMPTY),
            finalize(() => {
              this.syncing.set(false);
              this.initialLoading.set(false);
            }),
          );
        }),
      )
      .subscribe((res) => this.applyQueueResponse(res));

    this.subscriptions.add(pollSub);
  }

  ngOnDestroy(): void {
    this.subscriptions.unsubscribe();
    this.manualRefresh$.complete();
  }

  refreshNow(): void {
    this.sound.unlock();
    this.manualRefresh$.next();
  }

  /** Cor do relógio do card: verde <10min, amarelo <20min, vermelho ≥20min. */
  timerColor(order: KitchenQueueItem): TimerSeverity {
    if (order.late || order.elapsedMinutes >= 20) {
      return 'red';
    }
    if (order.elapsedMinutes >= 10) {
      return 'yellow';
    }
    return 'green';
  }

  orderTypeLabel(type: OrderType): string {
    return ORDER_TYPE_LABELS[type] ?? type;
  }

  orderTypeIcon(type: OrderType): string {
    switch (type) {
      case 'Delivery':
        return 'delivery_dining';
      case 'Pickup':
        return 'shopping_bag';
      default:
        return 'storefront';
    }
  }

  onFlavorImgError(event: Event): void {
    const target = event.target as HTMLElement | null;
    if (target) {
      target.style.display = 'none';
    }
  }

  unitLabel(unit: Unit): string {
    return UNIT_LABELS[unit]?.split(' ')[0] ?? unit;
  }

  /** Formata a fração do sabor (ex.: 50% → "1/2", 25% → "1/4", 100% → ""). */
  flavorFractionLabel(flavor: KitchenFlavor, totalFlavors: number): string {
    if (totalFlavors <= 1 && flavor.sharePercentage >= 99.9) {
      return '';
    }
    if (Math.abs(flavor.sharePercentage - 50) < 0.5) {
      return '1/2 (50%)';
    }
    if (Math.abs(flavor.sharePercentage - 25) < 0.5) {
      return '1/4 (25%)';
    }
    return `${flavor.sharePercentage}%`;
  }

  hasRecipe(order: KitchenQueueItem): boolean {
    return order.items.some((item) =>
      item.flavors.some((flavor) => flavor.recipe && flavor.recipe.length > 0),
    );
  }

  isExpanded(orderId: string): boolean {
    return this.expandedIds().has(orderId);
  }

  toggleRecipe(orderId: string): void {
    this.sound.unlock();
    this.expandedIds.update((prev) => {
      const next = new Set(prev);
      if (next.has(orderId)) {
        next.delete(orderId);
      } else {
        next.add(orderId);
      }
      return next;
    });
  }

  itemTitle(item: KitchenItem): string {
    if (item.comboName) {
      return `${item.quantity}x ${item.comboName}`;
    }
    if (item.sizeName) {
      return `${item.quantity}x Pizza ${item.sizeName}`;
    }
    return `${item.quantity}x Item`;
  }

  startOrder(order: KitchenQueueItem): void {
    this.sound.unlock();
    this.busyOrderId.set(order.id);
    this.service
      .start(order.id)
      .pipe(finalize(() => this.busyOrderId.set(null)))
      .subscribe({
        next: () => {
          this.snack.open(`Pedido #${order.code} em preparo.`, 'Fechar', {
            duration: 2500,
            panelClass: 'success-snack',
          });
          this.manualRefresh$.next();
        },
        error: () => undefined,
      });
  }

  markReady(order: KitchenQueueItem): void {
    this.sound.unlock();
    this.busyOrderId.set(order.id);
    this.service
      .ready(order.id)
      .pipe(finalize(() => this.busyOrderId.set(null)))
      .subscribe({
        next: () => {
          this.snack.open(
            `Pedido #${order.code} pronto! Baixa de estoque realizada.`,
            'Fechar',
            {
              duration: 3000,
              panelClass: 'success-snack',
            },
          );
          this.manualRefresh$.next();
        },
        error: () => undefined,
      });
  }

  finishOrder(order: KitchenQueueItem): void {
    this.sound.unlock();
    this.busyOrderId.set(order.id);
    this.service
      .finish(order.id)
      .pipe(finalize(() => this.busyOrderId.set(null)))
      .subscribe({
        next: () => {
          const msg =
            order.type === 'Delivery'
              ? `Pedido #${order.code} saiu para entrega.`
              : `Pedido #${order.code} concluído.`;
          this.snack.open(msg, 'Fechar', {
            duration: 2500,
            panelClass: 'success-snack',
          });
          this.manualRefresh$.next();
        },
        error: () => undefined,
      });
  }

  confirmCancel(order: KitchenQueueItem): void {
    this.sound.unlock();
    this.dialog
      .open(CancelOrderDialog, {
        data: {
          code: order.code,
          alreadyReady: order.status === 'Ready',
        },
        width: '460px',
        maxWidth: '95vw',
      })
      .afterClosed()
      .subscribe((reason: string | undefined) => {
        if (!reason) {
          return;
        }
        this.busyOrderId.set(order.id);
        this.service
          .cancel(order.id, reason)
          .pipe(finalize(() => this.busyOrderId.set(null)))
          .subscribe({
            next: () => {
              this.snack.open(`Pedido #${order.code} cancelado.`, 'Fechar', {
                duration: 3000,
                panelClass: 'warn-snack',
              });
              this.manualRefresh$.next();
            },
            error: () => undefined,
          });
      });
  }

  private applyQueueResponse(res: KitchenQueueResponse): void {
    const incoming = res.items ?? [];
    const currentReceivedIds = new Set(
      incoming.filter((o) => o.status === 'Received').map((o) => o.id),
    );

    if (this.hasLoadedOnce) {
      const hasBrandNewOrder = Array.from(currentReceivedIds).some(
        (id) => !this.knownReceivedIds.has(id),
      );
      if (hasBrandNewOrder) {
        this.sound.playNewOrderAlert();
      }
    }

    this.knownReceivedIds = currentReceivedIds;
    this.hasLoadedOnce = true;

    this.items.set(incoming);
    this.counts.set(
      res.counts ?? {
        received: incoming.filter((o) => o.status === 'Received').length,
        inPreparation: incoming.filter((o) => o.status === 'InPreparation').length,
        ready: incoming.filter((o) => o.status === 'Ready').length,
        total: incoming.length,
      },
    );
    const now = Date.now();
    this.lastSyncedAt.set(now);
    this.nowTick.set(now);
  }
}
