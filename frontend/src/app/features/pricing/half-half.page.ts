import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatRadioModule } from '@angular/material/radio';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { catchError, of, switchMap } from 'rxjs';

import { MixedPricingRequest, MixedPricingResult } from '../../core/models/pricing.models';
import { Product, Size } from '../../core/models/product.models';
import { MarginStatusChip } from '../../shared/components/margin-status-chip';
import { PricingResultCards } from '../../shared/components/pricing-result-cards';
import { BrlPipe } from '../../shared/pipes/brl.pipe';
import { ProductsService } from '../products/products.service';
import { PricingService } from './pricing.service';

type FlavorMode = 2 | 4;

/**
 * Pizza meio a meio (2 sabores) e de quatro sabores — docs/08 §3.7.
 * O mesmo seletor atende os dois modos: basta alternar a quantidade de sabores,
 * e a chamada muda de `/pricing/half-half` para `/pricing/four-flavors`.
 */
@Component({
  selector: 'pz-half-half-page',
  standalone: true,
  imports: [
    FormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatSelectModule,
    MatRadioModule,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    MatTableModule,
    MatSnackBarModule,
    MarginStatusChip,
    PricingResultCards,
    BrlPipe,
  ],
  templateUrl: './half-half.page.html',
  styleUrl: './half-half.page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HalfHalfPage {
  private readonly productsService = inject(ProductsService);
  private readonly pricing = inject(PricingService);
  private readonly snack = inject(MatSnackBar);

  readonly flavorColumns = ['name', 'cost', 'price'];

  readonly sizes = signal<Size[]>([]);
  readonly pizzas = signal<Product[]>([]);

  readonly sizeId = signal('');
  readonly mode = signal<FlavorMode>(2);
  readonly productIds = signal<string[]>(['', '']);

  readonly recalculating = this.pricing.recalculating;

  /** Índices dos campos de sabor visíveis (2 ou 4). */
  readonly flavorSlots = computed(() => Array.from({ length: this.mode() }, (_, i) => i));

  private readonly selection = computed(() => {
    const mode = this.mode();
    return {
      sizeId: this.sizeId(),
      mode,
      productIds: this.productIds().slice(0, mode),
    };
  });

  /** Calcula on-demand sempre que tamanho, modo ou sabores mudam. */
  readonly result = toSignal(
    toObservable(this.selection).pipe(
      switchMap((sel) => {
        if (!sel.sizeId || sel.productIds.some((id: string) => !id)) {
          return of(null);
        }
        const body: MixedPricingRequest = { sizeId: sel.sizeId, productIds: sel.productIds };
        const request$ = sel.mode === 2 ? this.pricing.halfHalf(body) : this.pricing.fourFlavors(body);
        return request$.pipe(catchError(() => of(null)));
      }),
    ),
    { initialValue: null as MixedPricingResult | null },
  );

  constructor() {
    this.productsService.sizes().subscribe({
      next: (items) => {
        this.sizes.set(items);
        if (items.length > 0) {
          this.sizeId.set(items[0].id);
        }
      },
      error: () => undefined,
    });
    this.productsService.products('Pizza').subscribe({
      next: (items) => this.pizzas.set(items),
      error: () => undefined,
    });
  }

  setMode(mode: FlavorMode): void {
    this.mode.set(mode);
    const ids = this.productIds();
    if (mode === 2) {
      this.productIds.set(ids.slice(0, 2));
    } else {
      this.productIds.set([...ids, '', ''].slice(0, 4));
    }
  }

  setFlavor(index: number, productId: string): void {
    const next = [...this.productIds()];
    next[index] = productId;
    this.productIds.set(next);
  }

  flavorName(index: number): string {
    const id = this.productIds()[index];
    return this.pizzas().find((p) => p.id === id)?.name ?? '—';
  }
}
