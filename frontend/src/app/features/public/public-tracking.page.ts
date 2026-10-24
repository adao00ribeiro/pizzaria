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
    <div class="pz-public-shell pz-tracking-shell">
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
            <!-- Card 1 (Topo): Código de acompanhamento, badge de status e saudação -->
            <mat-card class="section-card pz-tracking-code-card">
              <mat-card-content>
                <div class="pz-code-badge-row">
                  <div>
                    <span class="hint-text" style="font-size: 0.75rem; text-transform: uppercase; font-weight: 600; letter-spacing: 0.5px;">
                      Código de acompanhamento
                    </span>
                    <div class="pz-tracking-big-code">{{ tr.code }}</div>
                  </div>
                  <span class="pz-status-badge status-{{ tr.status }}">
                    {{ statusLabels[tr.status] }}
                  </span>
                </div>
                @if (tr.customerFirstName) {
                  <p class="pz-customer-greet">
                    Olá, <strong>{{ tr.customerFirstName }}</strong>! Acompanhe seu pedido em tempo real abaixo.
                  </p>
                }
                @if (tr.status === 'Cancelled') {
                  <div class="pz-cancel-banner" style="margin-top: 10px;">
                    <strong>Pedido cancelado:</strong>
                    {{ tr.cancellationReason || tr.cancelReason || 'Entre em contato com a pizzaria.' }}
                  </div>
                }
              </mat-card-content>
            </mat-card>

            <!-- Card 2: Progresso do pedido com os passos -->
            @if (tr.status !== 'Cancelled') {
              <mat-card class="section-card pz-tracking-progress-card">
                <mat-card-content>
                  <h3 class="pz-sub-title">
                    <mat-icon>timeline</mat-icon>
                    <span>Progresso do pedido</span>
                  </h3>
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

            <!-- Card 3: Resumo do pedido (Itens, Detalhes, Totais e Ações) -->
            <mat-card class="section-card pz-tracking-summary-card">
              <mat-card-content>
                <div class="pz-card-header-row">
                  <h3 class="pz-sub-title" style="margin: 0;">
                    <mat-icon>receipt_long</mat-icon>
                    <span>Resumo do pedido</span>
                  </h3>
                  <span class="pz-items-count-badge">{{ tr.items.length }} item(ns)</span>
                </div>

                <!-- Lista de itens com scroll interno suave -->
                <div class="pz-detail-items pz-scrollable-items" style="margin-top: 12px;">
                  @for (it of tr.items; track $index) {
                    <div class="pz-detail-item">
                      <div class="item-info">
                        <div class="item-name-row">
                          <span class="item-qty-pill">{{ it.quantity }}x</span>
                          <strong class="item-title">{{ getItemTitle(it) }}</strong>
                        </div>
                        @if (getItemFlavors(it); as flavors) {
                          <div class="item-flavors">
                            <span class="flavor-chip">{{ flavors }}</span>
                          </div>
                        }
                        @if (it.notes) {
                          <div class="pz-item-note">
                            <mat-icon>edit_note</mat-icon>
                            <span>Obs: {{ it.notes }}</span>
                          </div>
                        }
                      </div>
                      <strong class="item-price">{{ getItemPrice(it) }}</strong>
                    </div>
                  }
                </div>

                <!-- Detalhes do pedido (Tipo de entrega, Forma de pagamento, Horário) -->
                <div class="pz-order-meta-box">
                  <div class="pz-meta-line">
                    <mat-icon>{{ tr.type === 'Delivery' ? 'two_wheeler' : 'storefront' }}</mat-icon>
                    <span><strong>Tipo:</strong> {{ typeLabels[tr.type] }}</span>
                  </div>
                  @if (tr.paymentMethod) {
                    <div class="pz-meta-line">
                      <mat-icon>payments</mat-icon>
                      <span><strong>Pagamento:</strong> {{ paymentLabels[tr.paymentMethod] }}</span>
                    </div>
                  }
                  <div class="pz-meta-line">
                    <mat-icon>schedule</mat-icon>
                    <span><strong>Horário:</strong> {{ tr.placedAt | date: 'dd/MM/yyyy HH:mm' }}</span>
                  </div>
                </div>

                <!-- Totais (Subtotal, Taxa de entrega, Desconto, Total) -->
                <div class="pz-totals-bar">
                  <div><span>Subtotal:</span> <strong>{{ tr.subtotal | brl }}</strong></div>
                  @if (tr.deliveryFee > 0) {
                    <div><span>Taxa de entrega:</span> <strong>{{ tr.deliveryFee | brl }}</strong></div>
                  }
                  @if (tr.discount > 0) {
                    <div><span>Desconto:</span> <strong>- {{ tr.discount | brl }}</strong></div>
                  }
                  <div class="pz-grand-total">
                    <span>Total:</span>
                    <strong>{{ tr.total | brl }}</strong>
                  </div>
                </div>

                <!-- Botões de ação ("Fazer outro pedido" e "Atualizar agora") -->
                <div class="pz-tracking-actions">
                  <a mat-stroked-button [routerLink]="['/menu', slug]" class="pz-btn-block">
                    <mat-icon>restaurant_menu</mat-icon>
                    Fazer outro pedido
                  </a>
                  <button mat-button type="button" (click)="refresh()" class="pz-btn-block">
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

  slug = '';
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

  getItemTitle(it: any): string {
    if (it.comboName) return it.comboName;
    if (it.sizeName) return `Pizza ${it.sizeName}`;
    if (it.description) return it.description;
    if (it.flavors && it.flavors.length > 0) {
      if (it.flavors.length === 1) return it.flavors[0].productName;
      return 'Pizza meio a meio';
    }
    return 'Item do pedido';
  }

  getItemFlavors(it: any): string | null {
    if (!it.sizeName && !it.comboName) return null; // Bebida ou item sem tamanho não duplica o próprio nome como sabor
    if (it.flavorsLabel) return it.flavorsLabel;
    if (it.flavors && it.flavors.length > 0) {
      if (it.flavors.length === 1) return it.flavors[0].productName;
      return it.flavors
        .map((f: any) => `${Math.round(f.sharePercentage)}% ${f.productName}`)
        .join(' + ');
    }
    return null;
  }

  getItemPrice(it: any): string {
    const total = it.total ?? (it.unitPrice ? it.unitPrice * (it.quantity || 1) : 0);
    return total > 0
      ? total.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })
      : '';
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
