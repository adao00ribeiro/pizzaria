import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { finalize } from 'rxjs';

import { NonNullableFormControl, NonNullableFormGroup } from '../../core/forms.compat';
import { OrderType, PaymentMethod } from '../../core/models/common.models';
import { PAYMENT_METHOD_LABELS } from '../../core/models/labels';
import { CreatePublicOrderInput, PublicMenuDto } from '../../core/models/order.models';
import { BrlPipe } from '../../shared/pipes/brl.pipe';
import { PublicMenuService } from './public-menu.service';

@Component({
  selector: 'pz-public-checkout-page',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatCardModule,
    MatButtonModule,
    MatButtonToggleModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatIconModule,
    MatSnackBarModule,
    BrlPipe,
  ],
  template: `
    <div class="pz-public-shell">
      <header class="pz-public-header">
        <div class="pz-public-brand">
          <a mat-icon-button [routerLink]="['/menu', slug()]" aria-label="Voltar ao cardápio">
            <mat-icon>arrow_back</mat-icon>
          </a>
          <div>
            <h1>Finalizar pedido</h1>
            <span>{{ menu()?.pizzeriaName || slug() }}</span>
          </div>
        </div>
      </header>

      <main class="pz-public-container">
        @if (publicService.cart().length === 0) {
          <mat-card class="section-card" style="text-align: center;">
            <mat-card-content>
              <p class="hint-text">Sua sacola está vazia.</p>
              <a mat-flat-button color="primary" [routerLink]="['/menu', slug()]">
                Voltar ao cardápio
              </a>
            </mat-card-content>
          </mat-card>
        } @else {
          <!-- Sacola -->
          <mat-card class="section-card">
            <mat-card-content>
              <h2 class="card-title">Itens na sacola</h2>
              <div class="pz-cart-items">
                @for (item of publicService.cart(); track item.uid) {
                  <div class="pz-cart-row">
                    <div class="pz-cart-info">
                      <strong>{{ item.quantity }}x {{ item.label }}</strong>
                      @if (item.sublabel) {
                        <div class="hint-text">{{ item.sublabel }}</div>
                      }
                      @if (item.notes) {
                        <div class="pz-item-note">Obs: {{ item.notes }}</div>
                      }
                    </div>
                    <div class="pz-cart-controls">
                      <button
                        mat-mini-fab
                        color="basic"
                        type="button"
                        (click)="step(item.uid, -1)"
                      >
                        <mat-icon>remove</mat-icon>
                      </button>
                      <span class="pz-qty-badge">{{ item.quantity }}</span>
                      <button
                        mat-mini-fab
                        color="primary"
                        type="button"
                        (click)="step(item.uid, 1)"
                      >
                        <mat-icon>add</mat-icon>
                      </button>
                      <span class="pz-row-price">{{ item.unitPrice * item.quantity | brl }}</span>
                    </div>
                  </div>
                }
              </div>
            </mat-card-content>
          </mat-card>

          <!-- Dados do cliente e entrega -->
          <mat-card class="section-card">
            <mat-card-content>
              <h2 class="card-title">Seus dados e entrega</h2>
              <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
                <div class="pz-small-label">Como deseja receber?</div>
                <mat-button-toggle-group
                  formControlName="type"
                  class="pz-full-toggle"
                  aria-label="Tipo de pedido"
                >
                  <mat-button-toggle value="Pickup">Retirar no balcão</mat-button-toggle>
                  <mat-button-toggle value="Delivery">Entrega (Delivery)</mat-button-toggle>
                </mat-button-toggle-group>

                <div class="form-row" style="margin-top: 14px;">
                  <mat-form-field appearance="outline">
                    <mat-label>Seu nome *</mat-label>
                    <input matInput formControlName="customerName" maxlength="120" />
                  </mat-form-field>

                  <mat-form-field appearance="outline">
                    <mat-label>WhatsApp / Telefone *</mat-label>
                    <input matInput formControlName="customerPhone" maxlength="40" placeholder="(11) 99999-9999" />
                  </mat-form-field>
                </div>

                @if (form.controls.type.value === 'Delivery') {
                  <mat-form-field appearance="outline" class="full-width">
                    <mat-label>Endereço completo (rua, número, bairro, complemento) *</mat-label>
                    <input matInput formControlName="deliveryAddress" maxlength="300" />
                  </mat-form-field>
                }

                <div class="form-row">
                  <mat-form-field appearance="outline">
                    <mat-label>Forma de pagamento na entrega/retirada</mat-label>
                    <mat-select formControlName="paymentMethod">
                      @for (pm of paymentMethods; track pm) {
                        <mat-option [value]="pm">{{ paymentLabels[pm] }}</mat-option>
                      }
                    </mat-select>
                  </mat-form-field>

                  <mat-form-field appearance="outline">
                    <mat-label>Observações (ex.: troco para R$ 100)</mat-label>
                    <input matInput formControlName="notes" maxlength="400" />
                  </mat-form-field>
                </div>

                <div class="pz-totals-bar">
                  <div><span>Subtotal:</span> <strong>{{ publicService.subtotal() | brl }}</strong></div>
                  @if (deliveryFee() > 0) {
                    <div><span>Taxa de entrega:</span> <strong>{{ deliveryFee() | brl }}</strong></div>
                  }
                  <div class="pz-grand-total">
                    <span>Total a pagar:</span>
                    <strong>{{ grandTotal() | brl }}</strong>
                  </div>
                </div>

                <div class="form-actions" style="margin-top: 16px;">
                  <a mat-button [routerLink]="['/menu', slug()]">Adicionar mais itens</a>
                  <button
                    mat-flat-button
                    color="primary"
                    type="submit"
                    class="pz-touch-btn"
                    [disabled]="submitting()"
                  >
                    <mat-icon>send</mat-icon>
                    Enviar pedido ({{ grandTotal() | brl }})
                  </button>
                </div>
              </form>
            </mat-card-content>
          </mat-card>
        }
      </main>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PublicCheckoutPage {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly snack = inject(MatSnackBar);
  readonly publicService = inject(PublicMenuService);

  readonly slug = signal('');
  readonly menu = signal<PublicMenuDto | null>(null);
  readonly submitting = signal(false);

  readonly paymentMethods: PaymentMethod[] = ['Pix', 'Card', 'Cash'];
  readonly paymentLabels = PAYMENT_METHOD_LABELS;

  private readonly idempotencyKey =
    typeof crypto !== 'undefined' && 'randomUUID' in crypto
      ? crypto.randomUUID()
      : `${Date.now()}-${Math.random()}`;

  readonly form = new NonNullableFormGroup({
    type: new NonNullableFormControl<OrderType>('Delivery', [Validators.required]),
    customerName: new NonNullableFormControl('', [Validators.required]),
    customerPhone: new NonNullableFormControl('', [Validators.required]),
    deliveryAddress: new NonNullableFormControl(''),
    paymentMethod: new NonNullableFormControl<PaymentMethod>('Pix'),
    notes: new NonNullableFormControl(''),
  });

  constructor() {
    const slugParam = this.route.snapshot.paramMap.get('slug') ?? '';
    this.slug.set(slugParam);
    this.publicService.initCartForSlug(slugParam);

    const cached = this.publicService.menuCache();
    if (cached && cached.slug === slugParam) {
      this.menu.set(cached);
    } else {
      this.publicService.getMenu(slugParam).subscribe({
        next: (m) => {
          this.menu.set(m);
          this.publicService.menuCache.set(m);
        },
        error: () => undefined,
      });
    }
  }

  step(uid: string, delta: number): void {
    this.publicService.updateQuantity(this.slug(), uid, delta);
  }

  deliveryFee(): number {
    const m = this.menu();
    if (!m || this.form.controls.type.value !== 'Delivery') return 0;
    const sub = this.publicService.subtotal();
    return Number((m.deliveryFixedFee + sub * (m.deliveryFeePercentage / 100)).toFixed(2));
  }

  grandTotal(): number {
    return Number((this.publicService.subtotal() + this.deliveryFee()).toFixed(2));
  }

  submit(): void {
    if (this.publicService.cart().length === 0) return;
    const val = this.form.getRawValue();
    if (!val.customerName.trim() || !val.customerPhone.trim()) {
      this.snack.open('Preencha seu nome e telefone.', 'Fechar', { duration: 3000 });
      return;
    }
    if (val.type === 'Delivery' && !val.deliveryAddress.trim()) {
      this.snack.open('Preencha o endereço de entrega.', 'Fechar', { duration: 3000 });
      return;
    }

    const payload: CreatePublicOrderInput = {
      type: val.type,
      paymentMethod: val.paymentMethod,
      customerName: val.customerName.trim(),
      customerPhone: val.customerPhone.trim(),
      deliveryAddress: val.type === 'Delivery' ? val.deliveryAddress.trim() : null,
      notes: val.notes.trim() || null,
      items: this.publicService.cart().map((it) => ({
        sizeId: it.sizeId,
        comboId: it.comboId,
        quantity: it.quantity,
        notes: it.notes || null,
        flavors: it.flavors,
      })),
    };

    this.submitting.set(true);
    this.publicService
      .createOrder(this.slug(), payload, this.idempotencyKey)
      .pipe(finalize(() => this.submitting.set(false)))
      .subscribe({
        next: (created) => {
          const cleanCode = created.code.replace(/^#/, '');
          this.publicService.clearCart(this.slug());
          void this.router.navigate(['/acompanhar', this.slug(), cleanCode]);
        },
        error: () => undefined,
      });
  }
}
