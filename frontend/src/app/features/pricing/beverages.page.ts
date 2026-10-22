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
import { debounceTime, distinctUntilChanged, finalize, map, startWith } from 'rxjs';

import { extractApiErrorMessage } from '../../core/interceptors/error.interceptor';
import { Beverage } from '../../core/models/product.models';
import { BrlPipe } from '../../shared/pipes/brl.pipe';
import { PctPipe } from '../../shared/pipes/pct.pipe';
import { BeveragesService } from './beverages.service';
import { NonNullableFormControl } from '../../core/forms.compat';

/**
 * Precificação de bebidas — docs/08 §3.8.
 * Markup multiplicador por bebida, com preço sugerido recalculado no servidor
 * assim que o valor é confirmado.
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

  readonly columns = ['name', 'purchasePrice', 'markupMultiplier', 'suggestedPrice', 'marginPercentage', 'actions'];

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

  /** Valor de preço de compra e markup em edição por bebida (keyed pelo id). */
  readonly purchaseValues = signal<Record<string, number>>({});
  readonly markupValues = signal<Record<string, number>>({});

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
          const purchases: Record<string, number> = {};
          const markups: Record<string, number> = {};
          for (const b of items) {
            purchases[b.id] = b.purchasePrice;
            markups[b.id] = b.markupMultiplier;
          }
          this.purchaseValues.set(purchases);
          this.markupValues.set(markups);
        },
        error: () => undefined,
      });
  }

  setPurchasePrice(id: string, value: number): void {
    this.purchaseValues.update((current) => ({ ...current, [id]: value }));
  }

  setMarkup(id: string, value: number): void {
    this.markupValues.update((current) => ({ ...current, [id]: value }));
  }

  saveMarkup(beverage: Beverage): void {
    const purchase = this.purchaseValues()[beverage.id];
    const markup = this.markupValues()[beverage.id];
    if (purchase === undefined || Number.isNaN(purchase) || purchase < 0) {
      this.snack.open('Informe um preço de compra válido (≥ 0).', 'Fechar', {
        duration: 4000,
        panelClass: 'warn-snack',
      });
      return;
    }
    if (markup === undefined || Number.isNaN(markup) || markup <= 0) {
      this.snack.open('Informe um markup maior que zero.', 'Fechar', {
        duration: 4000,
        panelClass: 'warn-snack',
      });
      return;
    }
    this.savingId.set(beverage.id);
    this.service
      .updateMarkup(beverage.id, markup, purchase)
      .pipe(finalize(() => this.savingId.set('')))
      .subscribe({
        next: () => {
          this.snack.open(`Preço e markup de "${beverage.name}" atualizados.`, 'Fechar', {
            duration: 3000,
            panelClass: 'success-snack',
          });
          this.load();
        },
        error: (err: HttpErrorResponse) => {
          if (err.status === 409) {
            const body = err.error as { message?: string } | null;
            this.snack.open(body?.message ?? 'Não foi possível atualizar a bebida.', 'Fechar', {
              duration: 6000,
              panelClass: 'warn-snack',
            });
          }
        },
      });
  }
}
