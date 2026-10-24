import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTabsModule } from '@angular/material/tabs';
import { finalize } from 'rxjs';

import { NonNullableFormControl, NonNullableFormGroup } from '../../core/forms.compat';
import { OrderType, PaymentMethod } from '../../core/models/common.models';
import { ORDER_TYPE_LABELS, PAYMENT_METHOD_LABELS } from '../../core/models/labels';
import {
  CreateOrderFlavorInput,
  CreateOrderInput,
  OrderDto,
  PublicBeverageDto,
  PublicComboDto,
  PublicMenuDto,
} from '../../core/models/order.models';
import { BrlPipe } from '../../shared/pipes/brl.pipe';
import { OrdersService } from './orders.service';

export interface NewOrderDialogData {
  customerName?: string;
  customerPhone?: string;
  deliveryAddress?: string;
  notes?: string;
  type?: OrderType;
}

@Component({
  selector: 'pz-new-order-dialog',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatButtonToggleModule,
    MatIconModule,
    MatTabsModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
    BrlPipe,
  ],
  template: `
    <h2 mat-dialog-title class="pz-dialog-title">
      <mat-icon>point_of_sale</mat-icon>
      Novo pedido (Balcão / Telefone / WhatsApp)
    </h2>

    <mat-dialog-content class="pz-order-dialog-body">
      @if (loadingCatalog()) {
        <div class="loading">
          <mat-spinner diameter="36" />
        </div>
      } @else {
        @if (catalog(); as cat) {
          <div class="pz-order-grid">
            <!-- Coluna Esquerda: Construtor de Itens -->
            <div class="pz-order-builder">
              <mat-tab-group animationDuration="150ms">
                <!-- Aba 1: Pizzas (1, 2 ou 4 sabores) -->
                <mat-tab label="Pizzas">
                  <div class="pz-tab-pad">
                    <div class="form-row">
                      <mat-form-field appearance="outline">
                        <mat-label>Tamanho</mat-label>
                        <mat-select [value]="selectedSizeId()" (selectionChange)="onSizeChange($event.value)">
                          @for (sz of cat.sizes; track sz.id) {
                            <mat-option [value]="sz.id">{{ sz.name }} ({{ sz.diameterCm }}cm)</mat-option>
                          }
                        </mat-select>
                      </mat-form-field>

                      <div class="pz-flavor-count-box">
                        <span class="pz-small-label">Divisão de sabores</span>
                        <mat-button-toggle-group
                          [value]="flavorCount()"
                          (change)="onFlavorCountChange($event.value)"
                          aria-label="Quantidade de sabores"
                        >
                          <mat-button-toggle [value]="1">Inteira (1)</mat-button-toggle>
                          <mat-button-toggle [value]="2">Meio a meio (2)</mat-button-toggle>
                          <mat-button-toggle [value]="4">4 sabores</mat-button-toggle>
                        </mat-button-toggle-group>
                      </div>
                    </div>

                    <p class="hint-text" style="margin: 4px 0 8px;">
                      Selecione {{ flavorCount() }} sabor(es) abaixo (regra da pizzaria: prevalece o maior valor do tamanho):
                    </p>

                    <div class="pz-flavor-pills">
                      @for (pizza of availablePizzas(); track pizza.productId) {
                        <button
                          type="button"
                          class="pz-flavor-pill"
                          [class.selected]="isFlavorSelected(pizza.productId)"
                          (click)="toggleFlavor(pizza.productId)"
                        >
                          <div class="pz-flavor-thumb">
                            <div class="pz-thumb-fallback pizza-fallback">
                              <mat-icon>local_pizza</mat-icon>
                            </div>
                            @if (pizza.imageUrl) {
                              <img
                                [src]="pizza.imageUrl"
                                [alt]="pizza.name"
                                class="pz-thumb-img"
                                (error)="onImgError($event)"
                                loading="lazy"
                              />
                            }
                          </div>
                          <div class="pz-flavor-info">
                            <span class="pz-pill-name" [title]="pizza.name">{{ pizza.name }}</span>
                            <span class="pz-pill-price">{{ pizza.price | brl }}</span>
                          </div>
                          @if (isFlavorSelected(pizza.productId)) {
                            <mat-icon class="pz-pill-check">check_circle</mat-icon>
                          }
                        </button>
                      }
                    </div>

                    <div class="form-row" style="margin-top: 12px; align-items: center;">
                      <mat-form-field appearance="outline" style="flex: 1;">
                        <mat-label>Observação do item (ex.: sem cebola)</mat-label>
                        <input matInput [formControl]="itemNotesCtrl" maxlength="200" />
                      </mat-form-field>

                      <button
                        mat-flat-button
                        color="primary"
                        type="button"
                        class="pz-touch-btn"
                        [disabled]="selectedFlavorIds().length !== flavorCount()"
                        (click)="addPizzaToCart()"
                      >
                        <mat-icon>add_shopping_cart</mat-icon>
                        Adicionar ({{ currentPizzaPrice() | brl }})
                      </button>
                    </div>
                  </div>
                </mat-tab>

                <!-- Aba 2: Combos -->
                <mat-tab label="Combos ({{ cat.combos.length }})">
                  <div class="pz-tab-pad">
                    @if (cat.combos.length === 0) {
                      <p class="hint-text">Nenhum combo cadastrado.</p>
                    } @else {
                      <div class="pz-quick-list">
                        @for (combo of cat.combos; track combo.comboId || combo.id || $index) {
                          <div class="pz-quick-item">
                            <div class="pz-quick-item-leading">
                              <div class="pz-item-thumb">
                                <div class="pz-thumb-fallback combo-fallback">
                                  <mat-icon>fastfood</mat-icon>
                                </div>
                              </div>
                              <div class="pz-quick-item-info">
                                <strong class="pz-item-name">{{ combo.name }}</strong>
                                @if (combo.items.length > 0) {
                                  <div class="hint-text">
                                    @for (ci of combo.items; track $index; let last = $last) {
                                      {{ ci.quantity }}x {{ ci.productName }}{{ !last ? ' + ' : '' }}
                                    }
                                  </div>
                                }
                                <div class="pz-item-price">{{ combo.price | brl }}</div>
                              </div>
                            </div>
                            <button
                              mat-stroked-button
                              color="primary"
                              type="button"
                              class="pz-touch-btn"
                              (click)="addComboToCart(combo)"
                            >
                              <mat-icon>add</mat-icon> Adicionar
                            </button>
                          </div>
                        }
                      </div>
                    }
                  </div>
                </mat-tab>

                <!-- Aba 3: Bebidas -->
                <mat-tab label="Bebidas ({{ cat.beverages.length }})">
                  <div class="pz-tab-pad">
                    @if (cat.beverages.length === 0) {
                      <p class="hint-text">Nenhuma bebida cadastrada.</p>
                    } @else {
                      <div class="pz-quick-list">
                        @for (bev of cat.beverages; track bev.productId) {
                          <div class="pz-quick-item">
                            <div class="pz-quick-item-leading">
                              <div class="pz-item-thumb">
                                <div class="pz-thumb-fallback bev-fallback">
                                  <mat-icon>local_bar</mat-icon>
                                </div>
                                @if (bev.imageUrl) {
                                  <img
                                    [src]="bev.imageUrl"
                                    [alt]="bev.name"
                                    class="pz-thumb-img"
                                    (error)="onImgError($event)"
                                    loading="lazy"
                                  />
                                }
                              </div>
                              <div class="pz-quick-item-info">
                                <strong class="pz-item-name">{{ bev.name }}</strong>
                                <div class="pz-item-price">{{ bev.price | brl }}</div>
                              </div>
                            </div>
                            <button
                              mat-stroked-button
                              color="primary"
                              type="button"
                              class="pz-touch-btn"
                              (click)="addBeverageToCart(bev)"
                            >
                              <mat-icon>add</mat-icon> Adicionar
                            </button>
                          </div>
                        }
                      </div>
                    }
                  </div>
                </mat-tab>
              </mat-tab-group>
            </div>

            <!-- Coluna Direita: Dados do Atendimento e Carrinho -->
            <div class="pz-order-summary">
              <form [formGroup]="form" novalidate>
                <div class="pz-section-subtitle">Tipo de atendimento</div>
                <mat-button-toggle-group
                  formControlName="type"
                  class="pz-full-toggle"
                  aria-label="Tipo de pedido"
                >
                  @for (t of orderTypes; track t) {
                    <mat-button-toggle [value]="t">{{ orderTypeLabels[t] }}</mat-button-toggle>
                  }
                </mat-button-toggle-group>

                <div class="form-row" style="margin-top: 12px;">
                  <mat-form-field appearance="outline">
                    <mat-label>Cliente {{ form.controls.type.value !== 'Counter' ? '*' : '(opcional)' }}</mat-label>
                    <input matInput formControlName="customerName" maxlength="120" />
                  </mat-form-field>

                  <mat-form-field appearance="outline">
                    <mat-label>Telefone {{ form.controls.type.value !== 'Counter' ? '*' : '(opcional)' }}</mat-label>
                    <input matInput formControlName="customerPhone" maxlength="40" />
                  </mat-form-field>
                </div>

                @if (form.controls.type.value === 'Delivery') {
                  <mat-form-field appearance="outline" class="full-width">
                    <mat-label>Endereço de entrega *</mat-label>
                    <input matInput formControlName="deliveryAddress" maxlength="300" />
                  </mat-form-field>
                }

                <div class="form-row">
                  <mat-form-field appearance="outline">
                    <mat-label>Pagamento</mat-label>
                    <mat-select formControlName="paymentMethod">
                      @for (pm of paymentMethods; track pm) {
                        <mat-option [value]="pm">{{ paymentLabels[pm] }}</mat-option>
                      }
                    </mat-select>
                  </mat-form-field>

                  <mat-form-field appearance="outline">
                    <mat-label>Desconto (R$)</mat-label>
                    <input matInput type="number" min="0" step="0.5" formControlName="discount" />
                  </mat-form-field>
                </div>

                <mat-form-field appearance="outline" class="full-width">
                  <mat-label>Observações gerais do pedido</mat-label>
                  <input matInput formControlName="notes" maxlength="500" />
                </mat-form-field>
              </form>

              <!-- Lista de Itens no Carrinho -->
              <div class="pz-cart-box">
                <div class="pz-cart-header">
                  <strong>Itens do pedido ({{ cart().length }})</strong>
                  @if (cart().length > 0) {
                    <button mat-button color="warn" type="button" (click)="clearCart()">Limpar</button>
                  }
                </div>

                @if (cart().length === 0) {
                  <p class="hint-text" style="margin: 12px 0;">Adicione pizzas, combos ou bebidas ao lado.</p>
                } @else {
                  <div class="pz-cart-items">
                    @for (item of cart(); track item.uid) {
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
                            aria-label="Diminuir"
                            (click)="stepItem(item.uid, -1)"
                          >
                            <mat-icon>remove</mat-icon>
                          </button>
                          <span class="pz-qty-badge">{{ item.quantity }}</span>
                          <button
                            mat-mini-fab
                            color="primary"
                            type="button"
                            aria-label="Aumentar"
                            (click)="stepItem(item.uid, 1)"
                          >
                            <mat-icon>add</mat-icon>
                          </button>
                          <span class="pz-row-price">{{ item.unitPrice * item.quantity | brl }}</span>
                        </div>
                      </div>
                    }
                  </div>
                }

                <div class="pz-totals-bar">
                  <div><span>Subtotal:</span> <strong>{{ subtotal() | brl }}</strong></div>
                  @if (deliveryFee() > 0) {
                    <div><span>Taxa entrega:</span> <strong>{{ deliveryFee() | brl }}</strong></div>
                  }
                  @if (discountValue() > 0) {
                    <div><span>Desconto:</span> <strong>- {{ discountValue() | brl }}</strong></div>
                  }
                  <div class="pz-grand-total">
                    <span>Total:</span>
                    <strong>{{ grandTotal() | brl }}</strong>
                  </div>
                </div>
              </div>
            </div>
          </div>
        }
      }
    </mat-dialog-content>

    <mat-dialog-actions align="end">
      <button mat-button type="button" (click)="close()">Fechar (mantém rascunho)</button>
      <button
        mat-flat-button
        color="primary"
        type="button"
        class="pz-touch-btn"
        [disabled]="submitting() || cart().length === 0"
        (click)="submit()"
      >
        <mat-icon>check_circle</mat-icon>
        Confirmar pedido ({{ grandTotal() | brl }})
      </button>
    </mat-dialog-actions>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NewOrderDialog {
  private readonly ordersService = inject(OrdersService);
  private readonly dialogRef = inject(MatDialogRef<NewOrderDialog, OrderDto | undefined>);
  private readonly snack = inject(MatSnackBar);
  readonly dialogData = inject<NewOrderDialogData | null>(MAT_DIALOG_DATA, { optional: true });

  readonly orderTypes: OrderType[] = ['Counter', 'Pickup', 'Delivery'];
  readonly orderTypeLabels = ORDER_TYPE_LABELS;
  readonly paymentMethods: PaymentMethod[] = ['Cash', 'Card', 'Pix'];
  readonly paymentLabels = PAYMENT_METHOD_LABELS;

  readonly loadingCatalog = signal(true);
  readonly submitting = signal(false);
  readonly catalog = signal<PublicMenuDto | null>(null);

  readonly selectedSizeId = signal<string>('');
  readonly flavorCount = signal<1 | 2 | 4>(1);
  readonly selectedFlavorIds = signal<string[]>([]);
  readonly itemNotesCtrl = new FormControl<string>('', { nonNullable: true });

  readonly cart = this.ordersService.draftCart;
  readonly subtotal = this.ordersService.draftSubtotal;

  private readonly idempotencyKey =
    typeof crypto !== 'undefined' && 'randomUUID' in crypto
      ? crypto.randomUUID()
      : `${Date.now()}-${Math.random()}`;

  readonly form = new NonNullableFormGroup({
    type: new NonNullableFormControl<OrderType>(this.dialogData?.type ?? 'Counter', [Validators.required]),
    customerName: new NonNullableFormControl(this.dialogData?.customerName ?? ''),
    customerPhone: new NonNullableFormControl(this.dialogData?.customerPhone ?? ''),
    deliveryAddress: new NonNullableFormControl(this.dialogData?.deliveryAddress ?? ''),
    paymentMethod: new NonNullableFormControl<PaymentMethod>('Pix'),
    discount: new NonNullableFormControl<number>(0, [Validators.min(0)]),
    notes: new NonNullableFormControl(this.dialogData?.notes ?? ''),
  });

  onImgError(event: Event): void {
    const target = event.target as HTMLElement | null;
    if (target) {
      target.style.display = 'none';
    }
  }

  readonly availablePizzas = computed(() => {
    const cat = this.catalog();
    const sizeId = this.selectedSizeId();
    if (!cat || !sizeId) return [];
    return cat.pizzas
      .map((p) => {
        const match = p.pricesBySize.find((ps) => ps.sizeId === sizeId);
        return match
          ? {
              productId: p.productId,
              name: p.name,
              price: match.price,
              imageUrl: p.imageUrl ?? null,
            }
          : null;
      })
      .filter(
        (x): x is { productId: string; name: string; price: number; imageUrl: string | null } =>
          x !== null
      );
  });

  readonly currentPizzaPrice = computed(() => {
    const selected = this.selectedFlavorIds();
    const list = this.availablePizzas();
    if (selected.length === 0) return 0;
    return selected.reduce((max, id) => {
      const found = list.find((p) => p.productId === id);
      return found && found.price > max ? found.price : max;
    }, 0);
  });

  constructor() {
    this.ordersService
      .catalog()
      .pipe(finalize(() => this.loadingCatalog.set(false)))
      .subscribe({
        next: (cat) => {
          this.catalog.set(cat);
          if (cat.sizes.length > 0) {
            this.selectedSizeId.set(cat.sizes[0].id);
          }
        },
        error: () => undefined,
      });
  }

  onSizeChange(sizeId: string): void {
    this.selectedSizeId.set(sizeId);
    this.selectedFlavorIds.set([]);
  }

  onFlavorCountChange(count: 1 | 2 | 4): void {
    this.flavorCount.set(count);
    this.selectedFlavorIds.set([]);
  }

  isFlavorSelected(productId: string): boolean {
    return this.selectedFlavorIds().includes(productId);
  }

  toggleFlavor(productId: string): void {
    const current = this.selectedFlavorIds();
    if (current.includes(productId)) {
      this.selectedFlavorIds.set(current.filter((id) => id !== productId));
      return;
    }
    if (current.length >= this.flavorCount()) {
      this.snack.open(`Você já selecionou ${this.flavorCount()} sabor(es).`, 'Fechar', {
        duration: 2000,
      });
      return;
    }
    this.selectedFlavorIds.set([...current, productId]);
  }

  addPizzaToCart(): void {
    const cat = this.catalog();
    const sizeId = this.selectedSizeId();
    const selectedIds = this.selectedFlavorIds();
    const count = this.flavorCount();
    if (!cat || !sizeId || selectedIds.length !== count) return;

    const sizeObj = cat.sizes.find((s) => s.id === sizeId);
    const list = this.availablePizzas();
    const share = count === 1 ? 100 : count === 2 ? 50 : 25;

    const flavors: CreateOrderFlavorInput[] = selectedIds.map((id) => ({
      productId: id,
      sharePercentage: share,
    }));

    const names = selectedIds
      .map((id) => list.find((p) => p.productId === id)?.name ?? 'Sabor')
      .join(' / ');

    this.ordersService.addDraftItem({
      kind: 'pizza',
      label: `Pizza ${sizeObj?.name ?? ''}`,
      sublabel: count === 1 ? names : `Mista (${count} sabores): ${names}`,
      sizeId,
      comboId: null,
      flavors,
      unitPrice: this.currentPizzaPrice(),
      quantity: 1,
      notes: this.itemNotesCtrl.value.trim(),
    });

    this.selectedFlavorIds.set([]);
    this.itemNotesCtrl.setValue('');
  }

  addComboToCart(combo: PublicComboDto): void {
    this.ordersService.addDraftItem({
      kind: 'combo',
      label: combo.name,
      sublabel: combo.items.map((i) => `${i.quantity}x ${i.productName}`).join(' + '),
      sizeId: null,
      comboId: combo.comboId || combo.id || null,
      flavors: [],
      unitPrice: combo.price,
      quantity: 1,
      notes: '',
    });
  }

  addBeverageToCart(bev: PublicBeverageDto): void {
    this.ordersService.addDraftItem({
      kind: 'beverage',
      label: bev.name,
      sublabel: 'Bebida',
      sizeId: null,
      comboId: null,
      flavors: [{ productId: bev.productId, sharePercentage: 100 }],
      unitPrice: bev.price,
      quantity: 1,
      notes: '',
    });
  }

  stepItem(uid: string, delta: number): void {
    this.ordersService.updateDraftQuantity(uid, delta);
  }

  clearCart(): void {
    this.ordersService.clearDraft();
  }

  deliveryFee(): number {
    const cat = this.catalog();
    if (!cat || this.form.controls.type.value !== 'Delivery') return 0;
    const sub = this.subtotal();
    return Number((cat.deliveryFixedFee + sub * (cat.deliveryFeePercentage / 100)).toFixed(2));
  }

  discountValue(): number {
    const raw = Number(this.form.controls.discount.value ?? 0);
    return raw > 0 ? raw : 0;
  }

  grandTotal(): number {
    return Math.max(0, Number((this.subtotal() + this.deliveryFee() - this.discountValue()).toFixed(2)));
  }

  close(): void {
    this.dialogRef.close(undefined);
  }

  submit(): void {
    if (this.cart().length === 0) return;

    const val = this.form.getRawValue();
    if (val.type !== 'Counter') {
      if (!val.customerName.trim() || !val.customerPhone.trim()) {
        this.snack.open('Informe nome e telefone do cliente para Retirada/Entrega.', 'Fechar', {
          duration: 3500,
        });
        return;
      }
    }
    if (val.type === 'Delivery' && !val.deliveryAddress.trim()) {
      this.snack.open('Informe o endereço de entrega.', 'Fechar', { duration: 3500 });
      return;
    }
    const discount = Number(val.discount ?? 0);
    if (discount < 0 || discount > this.subtotal()) {
      this.snack.open('O desconto não pode ser maior que o subtotal.', 'Fechar', {
        duration: 3500,
      });
      return;
    }

    const payload: CreateOrderInput = {
      type: val.type,
      paymentMethod: val.paymentMethod,
      customerName: val.customerName.trim() || null,
      customerPhone: val.customerPhone.trim() || null,
      deliveryAddress: val.type === 'Delivery' ? val.deliveryAddress.trim() : null,
      notes: val.notes.trim() || null,
      discount,
      items: this.cart().map((it) => ({
        sizeId: it.sizeId,
        comboId: it.comboId,
        quantity: it.quantity,
        notes: it.notes || null,
        flavors: it.flavors,
      })),
    };

    this.submitting.set(true);
    this.ordersService
      .create(payload, this.idempotencyKey)
      .pipe(finalize(() => this.submitting.set(false)))
      .subscribe({
        next: (order) => {
          this.ordersService.clearDraft();
          this.dialogRef.close(order);
        },
        error: () => undefined,
      });
  }
}
