import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { finalize } from 'rxjs';

import {
  CreateOrderFlavorInput,
  PublicBeverageDto,
  PublicComboDto,
  PublicMenuDto,
} from '../../core/models/order.models';
import { BrlPipe } from '../../shared/pipes/brl.pipe';
import { PublicMenuService } from './public-menu.service';

export interface AvailablePizza {
  productId: string;
  name: string;
  price: number;
  imageUrl?: string | null;
}

@Component({
  selector: 'pz-public-menu-page',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatCardModule,
    MatButtonModule,
    MatButtonToggleModule,
    MatFormFieldModule,
    MatInputModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
    BrlPipe,
  ],
  template: `
    <div class="pz-public-shell">
      @if (loading()) {
        <div class="loading">
          <mat-spinner diameter="40" />
        </div>
      } @else if (notFound()) {
        <mat-card class="section-card" style="max-width: 480px; margin: 48px auto; text-align: center;">
          <mat-card-content>
            <mat-icon style="font-size: 48px; width: 48px; height: 48px; color: #c62828;">
              storefront
            </mat-icon>
            <h2>Cardápio não encontrado</h2>
            <p class="hint-text">
              Verifique se o endereço da pizzaria está correto ou peça o link atualizado.
            </p>
          </mat-card-content>
        </mat-card>
      } @else {
        @if (menu(); as m) {
          <header class="pz-public-header">
            <div class="pz-public-brand">
              <mat-icon>local_pizza</mat-icon>
              <div>
                <h1>{{ m.pizzeriaName }}</h1>
                <span>Cardápio online · Retirada ou Entrega</span>
              </div>
            </div>
          </header>

          <main class="pz-public-container">
            <!-- Montar Pizza (Inteira ou Meio a Meio) -->
            <mat-card class="section-card">
              <mat-card-content>
                <h2 class="card-title">Monte sua pizza</h2>

                <div class="pz-public-controls">
                  <div>
                    <div class="pz-small-label">1. Escolha o tamanho</div>
                    <mat-button-toggle-group
                      [value]="selectedSizeId()"
                      (change)="onSizeChange($event.value)"
                      aria-label="Tamanho da pizza"
                    >
                      @for (sz of m.sizes; track sz.id) {
                        <mat-button-toggle [value]="sz.id">
                          {{ sz.name }} ({{ sz.diameterCm }}cm)
                        </mat-button-toggle>
                      }
                    </mat-button-toggle-group>
                  </div>

                  <div>
                    <div class="pz-small-label">2. Quantidade de sabores</div>
                    <mat-button-toggle-group
                      [value]="flavorCount()"
                      (change)="onFlavorCountChange($event.value)"
                      aria-label="Sabores"
                    >
                      <mat-button-toggle [value]="1">1 Sabor (Inteira)</mat-button-toggle>
                      <mat-button-toggle [value]="2">2 Sabores (Meio a meio)</mat-button-toggle>
                    </mat-button-toggle-group>
                  </div>
                </div>

                <p class="hint-text" style="margin: 8px 0;">
                  Toque para escolher {{ flavorCount() }} sabor(es):
                </p>

                <div class="pz-menu-pizza-grid">
                  @for (pizza of availablePizzas(); track pizza.productId) {
                    <button
                      type="button"
                      class="pz-menu-pizza-card"
                      [class.selected]="isFlavorSelected(pizza.productId)"
                      (click)="toggleFlavor(pizza.productId)"
                    >
                      <div class="pz-menu-card-media">
                        <div class="pz-menu-card-fallback pizza-fallback">
                          <mat-icon>local_pizza</mat-icon>
                        </div>
                        @if (pizza.imageUrl) {
                          <img
                            [src]="pizza.imageUrl"
                            [alt]="pizza.name"
                            class="pz-menu-card-img"
                            (error)="onImgError($event)"
                            loading="lazy"
                          />
                        }
                        <div class="pz-menu-card-badge">
                          <mat-icon>check</mat-icon>
                        </div>
                      </div>
                      <div class="pz-menu-card-body">
                        <span class="pz-menu-card-title">{{ pizza.name }}</span>
                        <span class="pz-menu-card-price">{{ pizza.price | brl }}</span>
                      </div>
                    </button>
                  }
                </div>

                <div class="pz-public-add-row">
                  <mat-form-field appearance="outline" style="flex: 1;">
                    <mat-label>Observação (ex.: tirar cebola)</mat-label>
                    <input matInput [formControl]="notesCtrl" maxlength="200" />
                  </mat-form-field>

                  <button
                    mat-flat-button
                    color="primary"
                    type="button"
                    class="pz-touch-btn"
                    [disabled]="selectedFlavorIds().length !== flavorCount()"
                    (click)="addPizza()"
                  >
                    <mat-icon>add_shopping_cart</mat-icon>
                    Adicionar pizza ({{ currentPizzaPrice() | brl }})
                  </button>
                </div>
              </mat-card-content>
            </mat-card>

            <!-- Combos -->
            @if (m.combos.length > 0) {
              <mat-card class="section-card">
                <mat-card-content>
                  <h2 class="card-title">Combos especiais</h2>
                  <div class="pz-quick-list">
                    @for (combo of m.combos; track combo.comboId) {
                      <div class="pz-quick-item">
                        <div>
                          <strong>{{ combo.name }}</strong>
                          <div class="hint-text">
                            @for (ci of combo.items; track $index; let last = $last) {
                              {{ ci.quantity }}x {{ ci.productName }}{{ !last ? ' + ' : '' }}
                            }
                          </div>
                          <div class="pz-pill-price">{{ combo.price | brl }}</div>
                        </div>
                        <button
                          mat-flat-button
                          color="primary"
                          type="button"
                          (click)="addCombo(combo)"
                        >
                          <mat-icon>add</mat-icon> Adicionar
                        </button>
                      </div>
                    }
                  </div>
                </mat-card-content>
              </mat-card>
            }

            <!-- Bebidas -->
            @if (m.beverages.length > 0) {
              <mat-card class="section-card">
                <mat-card-content>
                  <h2 class="card-title">Bebidas</h2>
                  <div class="pz-menu-beverage-grid">
                    @for (bev of m.beverages; track bev.productId) {
                      <div class="pz-menu-beverage-item">
                        <div class="pz-menu-beverage-thumb">
                          <div class="pz-menu-card-fallback bev-fallback">
                            <mat-icon>local_drink</mat-icon>
                          </div>
                          @if (bev.imageUrl) {
                            <img
                              [src]="bev.imageUrl"
                              [alt]="bev.name"
                              class="pz-menu-card-img"
                              (error)="onImgError($event)"
                              loading="lazy"
                            />
                          }
                        </div>
                        <div class="pz-menu-beverage-details">
                          <strong class="pz-menu-card-title">{{ bev.name }}</strong>
                          <span class="pz-menu-card-price">{{ bev.price | brl }}</span>
                        </div>
                        <button
                          mat-flat-button
                          color="primary"
                          type="button"
                          class="pz-touch-btn"
                          (click)="addBeverage(bev)"
                        >
                          <mat-icon>add</mat-icon> Adicionar
                        </button>
                      </div>
                    }
                  </div>
                </mat-card-content>
              </mat-card>
            }
          </main>

          @if (publicService.totalCount() > 0) {
            <footer class="pz-sticky-cart">
              <div>
                <strong>{{ publicService.totalCount() }} item(ns) na sacola</strong>
                <div>Subtotal: {{ publicService.subtotal() | brl }}</div>
              </div>
              <a
                mat-flat-button
                color="primary"
                class="pz-touch-btn"
                [routerLink]="['/checkout', slug()]"
              >
                Ver sacola e finalizar
                <mat-icon>arrow_forward</mat-icon>
              </a>
            </footer>
          }
        }
      }
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PublicMenuPage {
  private readonly route = inject(ActivatedRoute);
  private readonly snack = inject(MatSnackBar);
  readonly publicService = inject(PublicMenuService);

  readonly slug = signal('');
  readonly loading = signal(true);
  readonly notFound = signal(false);
  readonly menu = signal<PublicMenuDto | null>(null);

  readonly selectedSizeId = signal('');
  readonly flavorCount = signal<1 | 2>(1);
  readonly selectedFlavorIds = signal<string[]>([]);
  readonly notesCtrl = new FormControl('', { nonNullable: true });

  onImgError(event: Event): void {
    const target = event.target as HTMLElement | null;
    if (target) {
      target.style.display = 'none';
    }
  }

  readonly availablePizzas = computed<AvailablePizza[]>(() => {
    const m = this.menu();
    const sizeId = this.selectedSizeId();
    if (!m || !sizeId) return [];
    const items: AvailablePizza[] = [];
    for (const p of m.pizzas) {
      const match = p.pricesBySize.find((ps) => ps.sizeId === sizeId);
      if (match) {
        items.push({
          productId: p.productId,
          name: p.name,
          price: match.price,
          imageUrl: p.imageUrl,
        });
      }
    }
    return items;
  });

  readonly currentPizzaPrice = computed(() => {
    const ids = this.selectedFlavorIds();
    const list = this.availablePizzas();
    return ids.reduce((max, id) => {
      const found = list.find((p) => p.productId === id);
      return found && found.price > max ? found.price : max;
    }, 0);
  });

  constructor() {
    const slugParam = this.route.snapshot.paramMap.get('slug') ?? '';
    this.slug.set(slugParam);
    this.publicService.initCartForSlug(slugParam);

    this.publicService
      .getMenu(slugParam)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (m) => {
          this.menu.set(m);
          this.publicService.menuCache.set(m);
          if (m.sizes.length > 0) {
            this.selectedSizeId.set(m.sizes[0].id);
          }
        },
        error: () => {
          this.notFound.set(true);
        },
      });
  }

  onSizeChange(sizeId: string): void {
    this.selectedSizeId.set(sizeId);
    this.selectedFlavorIds.set([]);
  }

  onFlavorCountChange(count: 1 | 2): void {
    this.flavorCount.set(count);
    this.selectedFlavorIds.set([]);
  }

  isFlavorSelected(productId: string): boolean {
    return this.selectedFlavorIds().includes(productId);
  }

  toggleFlavor(productId: string): void {
    const cur = this.selectedFlavorIds();
    if (cur.includes(productId)) {
      this.selectedFlavorIds.set(cur.filter((id) => id !== productId));
      return;
    }
    if (cur.length >= this.flavorCount()) {
      this.snack.open(`Você já selecionou ${this.flavorCount()} sabor(es).`, 'Fechar', {
        duration: 2000,
      });
      return;
    }
    this.selectedFlavorIds.set([...cur, productId]);
  }

  addPizza(): void {
    const m = this.menu();
    const sizeId = this.selectedSizeId();
    const ids = this.selectedFlavorIds();
    const count = this.flavorCount();
    if (!m || !sizeId || ids.length !== count) return;

    const sz = m.sizes.find((s) => s.id === sizeId);
    const list = this.availablePizzas();
    const share = count === 1 ? 100 : 50;
    const flavors: CreateOrderFlavorInput[] = ids.map((id) => ({
      productId: id,
      sharePercentage: share,
    }));
    const names = ids
      .map((id) => list.find((p) => p.productId === id)?.name ?? 'Sabor')
      .join(' / ');

    this.publicService.addItem(this.slug(), {
      kind: 'pizza',
      label: `Pizza ${sz?.name ?? ''}`,
      sublabel: count === 1 ? names : `Meio a meio: ${names}`,
      sizeId,
      comboId: null,
      flavors,
      unitPrice: this.currentPizzaPrice(),
      quantity: 1,
      notes: this.notesCtrl.value.trim(),
    });

    this.selectedFlavorIds.set([]);
    this.notesCtrl.setValue('');
    this.snack.open('Pizza adicionada à sacola!', 'Fechar', { duration: 2000 });
  }

  addCombo(combo: PublicComboDto): void {
    this.publicService.addItem(this.slug(), {
      kind: 'combo',
      label: combo.name,
      sublabel: combo.items.map((i) => `${i.quantity}x ${i.productName}`).join(' + '),
      sizeId: null,
      comboId: combo.comboId,
      flavors: [],
      unitPrice: combo.price,
      quantity: 1,
      notes: '',
    });
    this.snack.open('Combo adicionado à sacola!', 'Fechar', { duration: 2000 });
  }

  addBeverage(bev: PublicBeverageDto): void {
    this.publicService.addItem(this.slug(), {
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
    this.snack.open('Bebida adicionada à sacola!', 'Fechar', { duration: 2000 });
  }
}
