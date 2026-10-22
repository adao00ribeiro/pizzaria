import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

import { OrderStatus } from '../../core/models/common.models';
import { ORDER_STATUS_LABELS, ORDER_TYPE_LABELS, PAYMENT_METHOD_LABELS } from '../../core/models/labels';
import { PublicOrderTrackingDto } from '../../core/models/order.models';
import { BrlPipe } from '../../shared/pipes/brl.pipe';
import { PublicMenuService } from './public-menu.service';

@Component({
  selector: 'pz-public-tracking-page',
  standalone: true,
  imports: [
    DatePipe,
    RouterLink,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    BrlPipe,
  ],
  template: `
    <div class="pz-public-shell">
      @if (loading()) {
        <div class="loading">
          <mat-spinner diameter="40" />
        </div>
      } @else {
        @if (tracking(); as tr) {
          <header class="pz-public-header">
            <div class="pz-public-brand">
              <mat-icon>local_pizza</mat-icon>
              <div>
                <h1>{{ tr.pizzeriaName }}</h1>
                <span>Acompanhamento do pedido em tempo real</span>
              </div>
            </div>
          </header>

          <main class="pz-public-container">
            <mat-card class="section-card" style="text-align: center;">
              <mat-card-content>
                <div class="hint-text">Seu código de acompanhamento</div>
                <div class="pz-tracking-big-code">{{ tr.code }}</div>
                <span class="pz-status-badge status-{{ tr.status }}">
                  {{ statusLabels[tr.status] }}
                </span>
                @if (tr.customerFirstName) {
                  <p class="hint-text" style="margin-top: 8px;">
                    Olá, <strong>{{ tr.customerFirstName }}</strong>! ({{ typeLabels[tr.type] }})
                  </p>
                }
              </mat-card-content>
            </mat-card>

            @if (tr.status === 'Cancelled') {
              <mat-card class="section-card">
                <mat-card-content>
                  <div class="pz-cancel-banner">
                    <strong>Pedido cancelado:</strong>
                    {{ tr.cancellationReason || 'Entre em contato com a pizzaria.' }}
                  </div>
                </mat-card-content>
              </mat-card>
            } @else {
              <mat-card class="section-card">
                <mat-card-content>
                  <h2 class="card-title">Progresso</h2>
                  <div class="pz-tracking-steps">
                    @for (step of stepsFor(tr); track step) {
                      <div
                        class="pz-tracking-step"
                        [class.done]="isStepDone(tr.status, step)"
                        [class.active]="tr.status === step"
                      >
                        <mat-icon>
                          {{ isStepDone(tr.status, step) ? 'check_circle' : 'radio_button_unchecked' }}
                        </mat-icon>
                        <span>{{ statusLabels[step] }}</span>
                      </div>
                    }
                  </div>
                </mat-card-content>
              </mat-card>
            }

            <mat-card class="section-card">
              <mat-card-content>
                <h2 class="card-title">Resumo do pedido</h2>
                <div class="pz-detail-items">
                  @for (it of tr.items; track $index) {
                    <div class="pz-detail-item">
                      <div>
                        <strong>{{ it.quantity }}x {{ it.description }}</strong>
                        @if (it.flavorsLabel) {
                          <div class="hint-text">{{ it.flavorsLabel }}</div>
                        }
                        @if (it.notes) {
                          <div class="pz-item-note">Obs: {{ it.notes }}</div>
                        }
                      </div>
                      <strong>{{ it.total | brl }}</strong>
                    </div>
                  }
                </div>

                <div class="pz-totals-bar">
                  <div><span>Subtotal:</span> <strong>{{ tr.subtotal | brl }}</strong></div>
                  @if (tr.deliveryFee > 0) {
                    <div><span>Entrega:</span> <strong>{{ tr.deliveryFee | brl }}</strong></div>
                  }
                  @if (tr.discount > 0) {
                    <div><span>Desconto:</span> <strong>- {{ tr.discount | brl }}</strong></div>
                  }
                  <div class="pz-grand-total">
                    <span>Total:</span>
                    <strong>{{ tr.total | brl }}</strong>
                  </div>
                </div>

                <div class="form-actions" style="margin-top: 16px;">
                  <a mat-stroked-button [routerLink]="['/menu', tr.slug]">
                    Fazer outro pedido
                  </a>
                  <button mat-button type="button" (click)="refresh()">
                    <mat-icon>refresh</mat-icon> Atualizar agora
                  </button>
                </div>
              </mat-card-content>
            </mat-card>
          </main>
        }
      }
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PublicTrackingPage {
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);
  private readonly publicService = inject(PublicMenuService);

  readonly loading = signal(true);
  readonly tracking = signal<PublicOrderTrackingDto | null>(null);

  readonly statusLabels = ORDER_STATUS_LABELS;
  readonly typeLabels = ORDER_TYPE_LABELS;
  readonly paymentLabels = PAYMENT_METHOD_LABELS;

  private slug = '';
  private code = '';

  constructor() {
    this.slug = this.route.snapshot.paramMap.get('slug') ?? '';
    this.code = this.route.snapshot.paramMap.get('code') ?? '';
    this.refresh(true);

    // Polling a cada 15s (pausa quando aba oculta)
    const timer = setInterval(() => {
      if (typeof document !== 'undefined' && document.visibilityState === 'hidden') {
        return;
      }
      const cur = this.tracking();
      if (cur && (cur.status === 'Completed' || cur.status === 'Cancelled')) {
        return;
      }
      this.refresh(false);
    }, 15000);

    this.destroyRef.onDestroy(() => clearInterval(timer));
  }

  refresh(initial = false): void {
    if (initial) this.loading.set(true);
    this.publicService.trackOrder(this.slug, this.code).subscribe({
      next: (tr) => {
        this.tracking.set(tr);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
      },
    });
  }

  stepsFor(tr: PublicOrderTrackingDto): OrderStatus[] {
    return tr.type === 'Delivery'
      ? ['Received', 'InPreparation', 'Ready', 'OutForDelivery', 'Completed']
      : ['Received', 'InPreparation', 'Ready', 'Completed'];
  }

  isStepDone(current: OrderStatus, step: OrderStatus): boolean {
    const order: OrderStatus[] = ['Received', 'InPreparation', 'Ready', 'OutForDelivery', 'Completed'];
    return order.indexOf(current) >= order.indexOf(step);
  }
}
