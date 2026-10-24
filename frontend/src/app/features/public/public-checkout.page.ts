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
          <!-- Sacola de itens com scroll interno compacto -->
          <mat-card class="section-card">
            <mat-card-content>
              <div class="pz-card-header-row">
                <h2 class="card-title" style="margin: 0;">Itens na sacola</h2>
                <span class="pz-items-count-badge">{{ publicService.cart().length }} item(ns)</span>
              </div>

              <div class="pz-cart-items pz-scrollable-cart" style="margin-top: 8px;">
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

          <!-- Dados do cliente e entrega (Espaçoso e claro) -->
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
                    <input matInput formControlName="customerName" maxlength="120" placeholder="Ex: João da Silva" />
                  </mat-form-field>

                  <mat-form-field appearance="outline">
                    <mat-label>WhatsApp / Telefone *</mat-label>
                    <input
                      matInput
                      formControlName="customerPhone"
                      maxlength="16"
                      placeholder="(11) 99999-9999"
                      (input)="onPhoneInput($event)"
                    />
                  </mat-form-field>
                </div>

                @if (form.controls.type.value === 'Delivery') {
                  <div class="pz-address-section">
                    <div class="pz-section-subtitle">
                      <mat-icon>location_on</mat-icon>
                      <span>Endereço de entrega</span>
                    </div>

                    <!-- CEP com auto-busca ViaCEP -->
                    <div class="pz-cep-row">
                      <mat-form-field appearance="outline" class="pz-cep-field">
                        <mat-label>CEP (opcional)</mat-label>
                        <input
                          matInput
                          formControlName="cep"
                          maxlength="9"
                          placeholder="00000-000"
                          (input)="onCepInput($event)"
                        />
                        @if (loadingCep()) {
                          <mat-icon matSuffix class="spin-anim">sync</mat-icon>
                        } @else {
                          <button
                            mat-icon-button
                            matSuffix
                            type="button"
                            (click)="searchCep()"
                            [disabled]="!form.controls.cep.value"
                            aria-label="Buscar CEP"
                          >
                            <mat-icon>search</mat-icon>
                          </button>
                        }
                      </mat-form-field>
                      <span class="pz-cep-hint hint-text">
                        Digite o CEP para preencher rua e bairro automaticamente
                      </span>
                    </div>

                    <!-- Rua e Número -->
                    <div class="form-row pz-street-number-row">
                      <mat-form-field appearance="outline" class="pz-street-field">
                        <mat-label>Rua / Logradouro *</mat-label>
                        <input matInput formControlName="addressStreet" maxlength="200" placeholder="Ex: Rua das Flores" />
                      </mat-form-field>

                      <mat-form-field appearance="outline" class="pz-number-field">
                        <mat-label>Número *</mat-label>
                        <input matInput formControlName="addressNumber" maxlength="20" placeholder="123 ou S/N" />
                      </mat-form-field>
                    </div>

                    <!-- Bairro e Complemento -->
                    <div class="form-row">
                      <mat-form-field appearance="outline">
                        <mat-label>Bairro *</mat-label>
                        <input matInput formControlName="addressDistrict" maxlength="100" placeholder="Ex: Centro" />
                      </mat-form-field>

                      <mat-form-field appearance="outline">
                        <mat-label>Complemento / Referência</mat-label>
                        <input matInput formControlName="addressComplement" maxlength="120" placeholder="Ex: Apto 29B, Bloco 2" />
                      </mat-form-field>
                    </div>
                  </div>
                }

                <div class="form-row">
                  <mat-form-field appearance="outline">
                    <mat-label>Forma de pagamento</mat-label>
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

                <div class="pz-totals-bar" style="margin-top: 10px;">
                  <div><span>Subtotal:</span> <strong>{{ publicService.subtotal() | brl }}</strong></div>
                  @if (deliveryFee() > 0) {
                    <div><span>Taxa de entrega:</span> <strong>{{ deliveryFee() | brl }}</strong></div>
                  }
                  <div class="pz-grand-total">
                    <span>Total a pagar:</span>
                    <strong>{{ grandTotal() | brl }}</strong>
                  </div>
                </div>
              </form>
            </mat-card-content>
          </mat-card>
        }
      </main>

      <!-- Barra de Checkout Fixa / Sticky Footer sempre visível sem precisar de scroll -->
      @if (publicService.cart().length > 0) {
        <footer class="pz-fixed-checkout-bar">
          <div class="pz-fixed-bar-content">
            <div class="pz-fixed-summary">
              <span class="pz-fixed-label">Total a pagar</span>
              <span class="pz-fixed-total">{{ grandTotal() | brl }}</span>
              @if (form.controls.type.value === 'Delivery' && deliveryFee() > 0) {
                <span class="pz-fixed-sub">Taxa: {{ deliveryFee() | brl }}</span>
              }
            </div>

            <div class="pz-fixed-actions">
              <a mat-stroked-button [routerLink]="['/menu', slug()]" class="pz-fixed-more-btn">
                <mat-icon>add</mat-icon>
                <span>Mais itens</span>
              </a>
              <button
                mat-flat-button
                color="primary"
                type="button"
                (click)="submit()"
                class="pz-touch-btn pz-fixed-submit-btn"
                [disabled]="submitting()"
              >
                <mat-icon [class.spin-anim]="submitting()">{{ submitting() ? 'sync' : 'send' }}</mat-icon>
                <span>{{ submitting() ? 'Enviando...' : 'Enviar pedido' }}</span>
              </button>
            </div>
          </div>
        </footer>
      }
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
  readonly loadingCep = signal(false);

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
    cep: new NonNullableFormControl(''),
    addressStreet: new NonNullableFormControl(''),
    addressNumber: new NonNullableFormControl(''),
    addressDistrict: new NonNullableFormControl(''),
    addressComplement: new NonNullableFormControl(''),
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

  onPhoneInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    const clean = input.value.replace(/\D/g, '').slice(0, 11);
    let formatted = clean;
    if (clean.length > 2 && clean.length <= 6) {
      formatted = `(${clean.slice(0, 2)}) ${clean.slice(2)}`;
    } else if (clean.length > 6 && clean.length <= 10) {
      formatted = `(${clean.slice(0, 2)}) ${clean.slice(2, 6)}-${clean.slice(6)}`;
    } else if (clean.length > 10) {
      formatted = `(${clean.slice(0, 2)}) ${clean.slice(2, 7)}-${clean.slice(7, 11)}`;
    }
    this.form.controls.customerPhone.setValue(formatted, { emitEvent: false });
  }

  onCepInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    const clean = input.value.replace(/\D/g, '').slice(0, 8);
    const formatted = clean.length > 5 ? `${clean.slice(0, 5)}-${clean.slice(5)}` : clean;
    this.form.controls.cep.setValue(formatted, { emitEvent: false });
    if (clean.length === 8) {
      this.searchCep();
    }
  }

  searchCep(): void {
    const clean = this.form.controls.cep.value.replace(/\D/g, '');
    if (clean.length !== 8) {
      this.snack.open('Digite um CEP válido com 8 dígitos.', 'Fechar', { duration: 3000 });
      return;
    }
    this.loadingCep.set(true);
    this.publicService.lookupCep(clean).subscribe({
      next: (res) => {
        this.loadingCep.set(false);
        if (res.erro) {
          this.snack.open('CEP não encontrado. Preencha seu endereço manualmente.', 'Fechar', { duration: 3500 });
          return;
        }
        if (res.logradouro) {
          this.form.controls.addressStreet.setValue(res.logradouro);
        }
        if (res.bairro) {
          this.form.controls.addressDistrict.setValue(res.bairro);
        }
        if (res.complemento && !this.form.controls.addressComplement.value) {
          this.form.controls.addressComplement.setValue(res.complemento);
        }
        this.snack.open('Endereço preenchido com sucesso!', 'OK', { duration: 2500 });
      },
      error: () => {
        this.loadingCep.set(false);
        this.snack.open('Não foi possível consultar o CEP. Preencha manualmente.', 'Fechar', { duration: 3500 });
      },
    });
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

    if (val.type === 'Delivery') {
      if (!val.addressStreet.trim()) {
        this.snack.open('Informe a rua de entrega.', 'Fechar', { duration: 3000 });
        return;
      }
      if (!val.addressNumber.trim()) {
        this.snack.open('Informe o número do endereço de entrega.', 'Fechar', { duration: 3000 });
        return;
      }
      if (!val.addressDistrict.trim()) {
        this.snack.open('Informe o bairro de entrega.', 'Fechar', { duration: 3000 });
        return;
      }
    }

    const formattedDeliveryAddress = val.type === 'Delivery'
      ? `${val.addressStreet.trim()}, ${val.addressNumber.trim()} - ${val.addressDistrict.trim()}${val.addressComplement.trim() ? ` (${val.addressComplement.trim()})` : ''}`
      : null;

    const payload: CreatePublicOrderInput = {
      type: val.type,
      paymentMethod: val.paymentMethod,
      customerName: val.customerName.trim(),
      customerPhone: val.customerPhone.trim(),
      addressStreet: val.type === 'Delivery' ? val.addressStreet.trim() : null,
      addressNumber: val.type === 'Delivery' ? val.addressNumber.trim() : null,
      addressDistrict: val.type === 'Delivery' ? val.addressDistrict.trim() : null,
      addressComplement: val.type === 'Delivery' ? val.addressComplement.trim() || null : null,
      deliveryAddress: formattedDeliveryAddress,
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
