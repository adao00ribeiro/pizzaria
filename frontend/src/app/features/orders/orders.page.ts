import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { finalize, forkJoin } from 'rxjs';

import { OrderStatus, OrderType } from '../../core/models/common.models';
import { ORDER_STATUS_LABELS, ORDER_TYPE_LABELS, PAYMENT_METHOD_LABELS } from '../../core/models/labels';
import { OrderDto, OrderListItemDto, OrderSummaryDto } from '../../core/models/order.models';
import { BrlPipe } from '../../shared/pipes/brl.pipe';
import { CancelOrderDialog, CancelOrderDialogData } from '../kitchen/cancel-order.dialog';
import { NewOrderDialog } from './new-order.dialog';
import { OrdersService } from './orders.service';

@Component({
  selector: 'pz-orders-page',
  standalone: true,
  imports: [
    DatePipe,
    RouterLink,
    ReactiveFormsModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatTableModule,
    MatDialogModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
    MatTooltipModule,
    BrlPipe,
  ],
  templateUrl: './orders.page.html',
  styleUrl: './orders.page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OrdersPage {
  private readonly service = inject(OrdersService);
  private readonly dialog = inject(MatDialog);
  private readonly snack = inject(MatSnackBar);

  readonly loading = signal(true);
  readonly busyOrder = signal(false);
  readonly summary = signal<OrderSummaryDto | null>(null);
  readonly orders = signal<OrderListItemDto[]>([]);
  readonly total = signal(0);
  readonly selectedOrder = signal<OrderDto | null>(null);

  readonly statusCtrl = new FormControl<OrderStatus | ''>('', { nonNullable: true });
  readonly typeCtrl = new FormControl<OrderType | ''>('', { nonNullable: true });
  readonly searchCtrl = new FormControl<string>('', { nonNullable: true });

  readonly statuses: OrderStatus[] = [
    'Received',
    'InPreparation',
    'Ready',
    'OutForDelivery',
    'Completed',
    'Cancelled',
  ];
  readonly types: OrderType[] = ['Counter', 'Pickup', 'Delivery'];
  readonly statusLabels = ORDER_STATUS_LABELS;
  readonly typeLabels = ORDER_TYPE_LABELS;
  readonly paymentLabels = PAYMENT_METHOD_LABELS;

  readonly columns = ['code', 'placedAt', 'type', 'customer', 'itemsCount', 'total', 'status', 'actions'];

  constructor() {
    this.reload();
  }

  reload(): void {
    this.loading.set(true);
    forkJoin({
      summary: this.service.todaySummary(),
      list: this.service.list({
        status: this.statusCtrl.value,
        type: this.typeCtrl.value,
        search: this.searchCtrl.value,
        page: 1,
        pageSize: 50,
      }),
    })
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: ({ summary, list }) => {
          this.summary.set(summary);
          this.orders.set(list.items);
          this.total.set(list.total);
          const current = this.selectedOrder();
          if (current) {
            this.selectOrder(current.id);
          }
        },
        error: () => undefined,
      });
  }

  clearFilters(): void {
    this.statusCtrl.setValue('');
    this.typeCtrl.setValue('');
    this.searchCtrl.setValue('');
    this.reload();
  }

  openNewOrder(): void {
    const ref = this.dialog.open<NewOrderDialog, void, OrderDto | undefined>(NewOrderDialog, {
      width: '1080px',
      maxWidth: '96vw',
      disableClose: false,
    });
    ref.afterClosed().subscribe((created) => {
      if (created) {
        this.snack.open(`Pedido ${created.code} registrado com sucesso!`, 'Fechar', {
          duration: 3500,
          panelClass: 'success-snack',
        });
        this.selectedOrder.set(created);
        this.reload();
      }
    });
  }

  selectOrder(id: string): void {
    this.service.getById(id).subscribe({
      next: (dto) => this.selectedOrder.set(dto),
      error: () => undefined,
    });
  }

  closeDetail(): void {
    this.selectedOrder.set(null);
  }

  nextAction(order: OrderDto): { status: OrderStatus; label: string; icon: string } | null {
    switch (order.status) {
      case 'Received':
        return { status: 'InPreparation', label: 'Iniciar preparo', icon: 'local_fire_department' };
      case 'InPreparation':
        return { status: 'Ready', label: 'Marcar pronto (baixa estoque)', icon: 'done_all' };
      case 'Ready':
        return order.type === 'Delivery'
          ? { status: 'OutForDelivery', label: 'Saiu para entrega', icon: 'two_wheeler' }
          : { status: 'Completed', label: 'Concluir pedido', icon: 'check_circle' };
      case 'OutForDelivery':
        return { status: 'Completed', label: 'Concluir entrega', icon: 'check_circle' };
      default:
        return null;
    }
  }

  advanceStatus(order: OrderDto, target: OrderStatus): void {
    this.busyOrder.set(true);
    this.service
      .changeStatus(order.id, target)
      .pipe(finalize(() => this.busyOrder.set(false)))
      .subscribe({
        next: (updated) => {
          this.selectedOrder.set(updated);
          this.snack.open(`Pedido ${updated.code}: ${this.statusLabels[updated.status]}`, 'Fechar', {
            duration: 3000,
          });
          this.reload();
        },
        error: () => undefined,
      });
  }

  typeLabel(type: OrderType): string {
    return this.typeLabels[type] ?? type;
  }

  statusLabel(status: OrderStatus): string {
    return this.statusLabels[status] ?? status;
  }

  orderTypeIcon(type: OrderType): string {
    switch (type) {
      case 'Counter':
        return 'storefront';
      case 'Pickup':
        return 'shopping_bag';
      case 'Delivery':
        return 'moped';
      default:
        return 'receipt';
    }
  }

  statusIcon(status: OrderStatus): string {
    switch (status) {
      case 'Received':
        return 'inbox';
      case 'InPreparation':
        return 'local_fire_department';
      case 'Ready':
        return 'done_all';
      case 'OutForDelivery':
        return 'two_wheeler';
      case 'Completed':
        return 'check_circle';
      case 'Cancelled':
        return 'cancel';
      default:
        return 'help_outline';
    }
  }

  filterByStatus(status: OrderStatus | ''): void {
    if (this.statusCtrl.value === status) {
      this.statusCtrl.setValue('');
    } else {
      this.statusCtrl.setValue(status);
    }
    this.reload();
  }

  readyAndDeliveryCount(s: OrderSummaryDto | null): number {
    if (!s) return 0;
    const ready = s.ready ?? s.readyCount ?? 0;
    const delivery = s.outForDelivery ?? s.outForDeliveryCount ?? 0;
    return ready + delivery;
  }

  promptCancel(order: OrderDto): void {
    const data: CancelOrderDialogData = {
      code: order.code,
      alreadyReady: order.status === 'Ready' || order.status === 'OutForDelivery',
    };
    const ref = this.dialog.open<CancelOrderDialog, CancelOrderDialogData, string | undefined>(
      CancelOrderDialog,
      { width: '440px', data },
    );
    ref.afterClosed().subscribe((reason) => {
      if (!reason) return;
      this.busyOrder.set(true);
      this.service
        .cancel(order.id, reason)
        .pipe(finalize(() => this.busyOrder.set(false)))
        .subscribe({
          next: (updated) => {
            this.selectedOrder.set(updated);
            this.snack.open(`Pedido ${updated.code} cancelado.`, 'Fechar', { duration: 3000 });
            this.reload();
          },
          error: () => undefined,
        });
    });
  }
}
