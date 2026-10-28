import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatChipsModule } from '@angular/material/chips';
import { debounceTime, distinctUntilChanged, finalize, map, startWith } from 'rxjs';

import { extractApiErrorMessage } from '../../core/interceptors/error.interceptor';
import { Beverage } from '../../core/models/product.models';
import { BrlPipe } from '../../shared/pipes/brl.pipe';
import { PctPipe } from '../../shared/pipes/pct.pipe';
import { BeveragesService } from './beverages.service';
import { NonNullableFormControl } from '../../core/forms.compat';

/**
 * Precificação e estoque de bebidas — docs/08 §3.8.
 * Markup multiplicador por bebida, estoque e alerta de reposição recalculados no servidor.
 */
@Component({
  selector: 'pz-beverages-page',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatTableModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatChipsModule,
    MatTooltipModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
    RouterLink,
    BrlPipe,
    PctPipe,
  ],
  templateUrl: './beverages.page.html',
  styleUrl: './beverages.page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BeveragesPage {
  private readonly service = inject(BeveragesService);
  private readonly snack = inject(MatSnackBar);

  readonly columns = [
    'name',
    'purchasePrice',
    'markupMultiplier',
    'suggestedPrice',
    'marginPercentage',
    'currentStock',
    'minimumStock',
    'status',
    'actions',
  ];

  readonly loading = signal(true);
  readonly beverages = signal<Beverage[]>([]);
  readonly savingId = signal('');

  readonly searchControl = new NonNullableFormControl('');
  readonly search = toSignal(
    this.searchControl.valueChanges.pipe(
      startWith(''),
      debounceTime(250),
      distinctUntilChanged(),
      map((t) => t.trim().toLowerCase()),
    ),
    { initialValue: '' },
  );

  /** Valores em edição por bebida (keyed pelo id) — suporta string durante digitação para não travar o input. */
  readonly purchaseValues = signal<Record<string, number | string | undefined>>({});
  readonly markupValues = signal<Record<string, number | string | undefined>>({});
  readonly currentStockValues = signal<Record<string, number | string | undefined>>({});
  readonly minimumStockValues = signal<Record<string, number | string | undefined>>({});

  readonly filtered = computed(() => {
    const term = this.search();
    const list = this.beverages();
    return term ? list.filter((b) => b.name.toLowerCase().includes(term)) : list;
  });

  constructor() {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.service
      .list()
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (items) => {
          this.beverages.set(items);
          const purchases: Record<string, number | string> = {};
          const markups: Record<string, number | string> = {};
          const stocks: Record<string, number | string> = {};
          const minStocks: Record<string, number | string> = {};
          for (const b of items) {
            purchases[b.id] = b.purchasePrice;
            markups[b.id] = b.markupMultiplier;
            stocks[b.id] = b.currentStock ?? 0;
            minStocks[b.id] = b.minimumStock ?? 0;
          }
          this.purchaseValues.set(purchases);
          this.markupValues.set(markups);
          this.currentStockValues.set(stocks);
          this.minimumStockValues.set(minStocks);
        },
        error: () => undefined,
      });
  }

  parseSafeNumber(val: unknown, fallback: number = 0): number {
    if (val === null || val === undefined || val === '') {
      return fallback;
    }
    if (typeof val === 'number') {
      return Number.isNaN(val) ? fallback : val;
    }
    const str = String(val).replace(',', '.').trim();
    if (!str) {
      return fallback;
    }
    const parsed = Number(str);
    return Number.isNaN(parsed) ? fallback : parsed;
  }

  setPurchasePrice(id: string, raw: unknown): void {
    const val = typeof raw === 'string' ? raw : (raw != null ? String(raw) : '');
    this.purchaseValues.update((current) => ({ ...current, [id]: val }));
  }

  setMarkup(id: string, raw: unknown): void {
    const val = typeof raw === 'string' ? raw : (raw != null ? String(raw) : '');
    this.markupValues.update((current) => ({ ...current, [id]: val }));
  }

  setCurrentStock(id: string, raw: unknown): void {
    const val = typeof raw === 'string' ? raw : (raw != null ? String(raw) : '');
    this.currentStockValues.update((current) => ({ ...current, [id]: val }));
  }

  setMinimumStock(id: string, raw: unknown): void {
    const val = typeof raw === 'string' ? raw : (raw != null ? String(raw) : '');
    this.minimumStockValues.update((current) => ({ ...current, [id]: val }));
  }

  isBelowMinimum(b: Beverage): boolean {
    const cur = this.parseSafeNumber(this.currentStockValues()[b.id], b.currentStock ?? 0);
    const min = this.parseSafeNumber(this.minimumStockValues()[b.id], b.minimumStock ?? 0);
    return min > 0 && cur <= min;
  }

  saveMarkup(beverage: Beverage): void {
    const purchase = this.parseSafeNumber(this.purchaseValues()[beverage.id], beverage.purchasePrice ?? 0);
    const markup = this.parseSafeNumber(this.markupValues()[beverage.id], beverage.markupMultiplier ?? 2.0);
    const currentStock = this.parseSafeNumber(this.currentStockValues()[beverage.id], beverage.currentStock ?? 0);
    const minimumStock = this.parseSafeNumber(this.minimumStockValues()[beverage.id], beverage.minimumStock ?? 0);

    if (purchase < 0) {
      this.snack.open('Informe um preço de compra válido (≥ 0).', 'Fechar', {
        duration: 4000,
        panelClass: 'warn-snack',
      });
      return;
    }
    if (markup <= 0) {
      this.snack.open('Informe um markup maior que zero.', 'Fechar', {
        duration: 4000,
        panelClass: 'warn-snack',
      });
      return;
    }
    if (currentStock < 0) {
      this.snack.open('Quantidade em estoque não pode ser negativa.', 'Fechar', {
        duration: 4000,
        panelClass: 'warn-snack',
      });
      return;
    }
    if (minimumStock < 0) {
      this.snack.open('Estoque mínimo não pode ser negativo.', 'Fechar', {
        duration: 4000,
        panelClass: 'warn-snack',
      });
      return;
    }

    this.savingId.set(beverage.id);
    this.service
      .updateMarkup(beverage.id, markup, purchase, currentStock, minimumStock)
      .pipe(finalize(() => this.savingId.set('')))
      .subscribe({
        next: (updated) => {
          this.snack.open(`Preço e estoque de "${beverage.name}" atualizados.`, 'Fechar', {
            duration: 3000,
            panelClass: 'success-snack',
          });

          if (updated && updated.id) {
            this.beverages.update((list) =>
              list.map((item) => (item.id === updated.id ? updated : item))
            );
            this.purchaseValues.update((m) => ({ ...m, [updated.id]: updated.purchasePrice }));
            this.markupValues.update((m) => ({ ...m, [updated.id]: updated.markupMultiplier }));
            this.currentStockValues.update((m) => ({ ...m, [updated.id]: updated.currentStock ?? 0 }));
            this.minimumStockValues.update((m) => ({ ...m, [updated.id]: updated.minimumStock ?? 0 }));
          } else {
            this.load();
          }
        },
        error: (err: HttpErrorResponse) => {
          const msg = extractApiErrorMessage(err) ?? 'Não foi possível atualizar a bebida.';
          this.snack.open(msg, 'Fechar', {
            duration: 6000,
            panelClass: 'warn-snack',
          });
        },
      });
  }
}
