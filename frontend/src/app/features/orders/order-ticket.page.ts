import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { finalize } from 'rxjs';

import { ORDER_STATUS_LABELS, ORDER_TYPE_LABELS, PAYMENT_METHOD_LABELS } from '../../core/models/labels';
import { OrderTicketDto } from '../../core/models/order.models';
import { BrlPipe } from '../../shared/pipes/brl.pipe';
import { OrdersService } from './orders.service';

@Component({
  selector: 'pz-order-ticket-page',
  standalone: true,
  imports: [
    DatePipe,
    RouterLink,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    BrlPipe,
  ],
  template: `
    <div class="pz-ticket-wrapper">
      <div class="pz-ticket-actions no-print">
        <a mat-stroked-button routerLink="/pedidos">
          <mat-icon>arrow_back</mat-icon> Voltar para pedidos
        </a>
        <button mat-flat-button color="primary" type="button" (click)="print()">
          <mat-icon>print</mat-icon> Imprimir comanda
        </button>
      </div>

      @if (loading()) {
        <div class="loading no-print">
          <mat-spinner diameter="36" />
        </div>
      } @else {
        @if (ticket(); as t) {
          <article class="pz-thermal-ticket" aria-label="Comanda para impressão">
            <header class="pz-ticket-header">
              <h1>{{ t.pizzeriaName }}</h1>
              <div class="pz-ticket-code">PEDIDO {{ t.code }}</div>
              <div class="pz-ticket-meta">
                <span>{{ typeLabels[t.type] }}</span> ·
                <span>{{ statusLabels[t.status] }}</span>
              </div>
              <div class="pz-ticket-date">{{ t.placedAt | date: 'dd/MM/yyyy HH:mm' }}</div>
            </header>

            @if (t.customerName || t.customerPhone || t.deliveryAddress) {
              <section class="pz-ticket-section">
                @if (t.customerName) {
                  <div><strong>Cliente:</strong> {{ t.customerName }}</div>
                }
                @if (t.customerPhone) {
                  <div><strong>Fone:</strong> {{ t.customerPhone }}</div>
                }
                @if (t.deliveryAddress) {
                  <div><strong>Entrega:</strong> {{ t.deliveryAddress }}</div>
                }
              </section>
            }

            <section class="pz-ticket-section">
              <div class="pz-ticket-subtitle">ITENS DO PEDIDO</div>
              @for (item of t.items; track $index) {
                <div class="pz-ticket-item">
                  <div class="pz-ticket-item-line">
                    <strong>{{ item.quantity }}x {{ item.description }}</strong>
                    <span>{{ item.total | brl }}</span>
                  </div>
                  @if (item.flavorsLabel) {
                    <div class="pz-ticket-item-sub">{{ item.flavorsLabel }}</div>
                  }
                  @if (item.notes) {
                    <div class="pz-ticket-item-note">OBS: {{ item.notes }}</div>
                  }
                </div>
              }
            </section>

            @if (t.notes) {
              <section class="pz-ticket-section">
                <strong>OBS GERAL:</strong> {{ t.notes }}
              </section>
            }

            <section class="pz-ticket-section pz-ticket-totals">
              <div class="pz-ticket-line">
                <span>Subtotal</span>
                <span>{{ t.subtotal | brl }}</span>
              </div>
              @if (t.deliveryFee > 0) {
                <div class="pz-ticket-line">
                  <span>Taxa de entrega</span>
                  <span>{{ t.deliveryFee | brl }}</span>
                </div>
              }
              @if (t.discount > 0) {
                <div class="pz-ticket-line">
                  <span>Desconto</span>
                  <span>- {{ t.discount | brl }}</span>
                </div>
              }
              <div class="pz-ticket-line pz-ticket-grand">
                <strong>TOTAL</strong>
                <strong>{{ t.total | brl }}</strong>
              </div>
              @if (t.paymentMethod) {
                <div class="pz-ticket-line">
                  <span>Pagamento</span>
                  <span>{{ paymentLabels[t.paymentMethod] }}</span>
                </div>
              }
            </section>

            <footer class="pz-ticket-footer">
              {{ t.disclaimer }}
            </footer>
          </article>
        }
      }
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OrderTicketPage {
  private readonly route = inject(ActivatedRoute);
  private readonly ordersService = inject(OrdersService);

  readonly loading = signal(true);
  readonly ticket = signal<OrderTicketDto | null>(null);

  readonly typeLabels = ORDER_TYPE_LABELS;
  readonly statusLabels = ORDER_STATUS_LABELS;
  readonly paymentLabels = PAYMENT_METHOD_LABELS;

  constructor() {
    const id = this.route.snapshot.paramMap.get('id') ?? '';
    if (id) {
      this.ordersService
        .getTicket(id)
        .pipe(finalize(() => this.loading.set(false)))
        .subscribe({
          next: (t) => this.ticket.set(t),
          error: () => undefined,
        });
    } else {
      this.loading.set(false);
    }
  }

  print(): void {
    window.print();
  }
}
