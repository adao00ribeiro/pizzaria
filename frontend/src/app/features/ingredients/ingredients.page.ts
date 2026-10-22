import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { finalize } from 'rxjs';

import { PAGE_SIZE, Unit } from '../../core/models/common.models';
import { UNIT_LABELS } from '../../core/models/labels';
import { Ingredient, IngredientRequest } from '../../core/models/ingredient.models';
import { ConfirmDialog, ConfirmDialogData } from '../../shared/components/confirm-dialog';
import { BrlPipe } from '../../shared/pipes/brl.pipe';
import { PctPipe } from '../../shared/pipes/pct.pipe';
import {
  IngredientFormDialog,
  IngredientUsagesDialog,
  UsagesDialogData,
} from './ingredient-form.dialog';
import { ReplenishStockDialog, ReplenishStockDialogData } from './replenish-stock.dialog';
import { IngredientsService } from './ingredients.service';
import { NonNullableFormControl } from '../../core/forms.compat';

/**
 * Ingredientes: mat-table com busca client-side sobre a página,
 * diálogo de cadastro com custo por unidade ao vivo (docs/08 §3.3)
 * e diálogo rápido de reposição / entrada de estoque.
 */
@Component({
  selector: 'pz-ingredients-page',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatTableModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatTooltipModule,
    MatPaginatorModule,
    MatProgressSpinnerModule,
    MatDialogModule,
    MatSnackBarModule,
    BrlPipe,
    PctPipe,
  ],
  templateUrl: './ingredients.page.html',
  styleUrl: './ingredients.page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class IngredientsPage {
  private readonly service = inject(IngredientsService);
  private readonly dialog = inject(MatDialog);
  private readonly snack = inject(MatSnackBar);

  readonly pageSize = PAGE_SIZE;
  readonly columns = ['name', 'purchase', 'unitCost', 'loss', 'stock', 'actions'];
  readonly unitLabels = UNIT_LABELS;

  readonly loading = signal(true);
  readonly items = signal<Ingredient[]>([]);
  readonly total = signal(0);
  readonly page = signal(1);
  readonly search = signal('');

  readonly searchControl = new NonNullableFormControl('');

  /** Filtro client-side sobre a página carregada (docs/08 §3.3). */
  readonly filtered = computed(() => {
    const term = this.search();
    const list = this.items();
    if (!term) {
      return list;
    }
    return list.filter((i) => i.name.toLowerCase().includes(term));
  });

  constructor() {
    this.searchControl.valueChanges.pipe(takeUntilDestroyed()).subscribe((term) => {
      this.search.set(term.trim().toLowerCase());
    });
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.service
      .list({ page: this.page() })
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (res) => {
          this.items.set(res.items);
          this.total.set(res.total);
        },
        error: () => undefined,
      });
  }

  onPage(event: PageEvent): void {
    this.page.set(event.pageIndex + 1);
    this.load();
  }

  stockClass(ingredient: Ingredient): boolean {
    return ingredient.currentStock < ingredient.minimumStock;
  }

  openReplenish(ingredient?: Ingredient): void {
    this.dialog
      .open<ReplenishStockDialog, ReplenishStockDialogData, boolean>(
        ReplenishStockDialog,
        {
          data: { ingredient },
          width: 'min(620px, 96vw)',
          autoFocus: 'first',
        },
      )
      .afterClosed()
      .subscribe((saved) => {
        if (saved) {
          this.load();
        }
      });
  }

  openForm(ingredient?: Ingredient): void {
    this.dialog
      .open<IngredientFormDialog, { ingredient?: Ingredient }, IngredientRequest | undefined>(
        IngredientFormDialog,
        {
          data: { ingredient },
          width: 'min(680px, 96vw)',
          autoFocus: 'first',
        },
      )
      .afterClosed()
      .subscribe((body) => {
        if (!body) {
          return;
        }
        const request$ = ingredient
          ? this.service.update(ingredient.id, body)
          : this.service.create(body);
        request$.subscribe({
          next: () => {
            this.snack.open('Ingrediente salvo com sucesso.', 'Fechar', {
              duration: 3000,
              panelClass: 'success-snack',
            });
            this.load();
          },
          error: () => undefined,
        });
      });
  }

  remove(ingredient: Ingredient): void {
    const data: ConfirmDialogData = {
      title: 'Excluir ingrediente',
      message: `Excluir o ingrediente "${ingredient.name}"? Preparos e fichas que o utilizam podem ser afetados.`,
      confirmLabel: 'Excluir',
    };
    this.dialog
      .open(ConfirmDialog, { data })
      .afterClosed()
      .subscribe((confirmed: boolean | undefined) => {
        if (!confirmed) {
          return;
        }
        this.service.remove(ingredient.id).subscribe({
          next: () => {
            this.snack.open('Ingrediente excluído.', 'Fechar', {
              duration: 3000,
              panelClass: 'success-snack',
            });
            this.load();
          },
          error: (err: HttpErrorResponse) => {
            if (err.status === 409) {
              // O ErrorInterceptor não mostra 409: exibimos o conflito com usos
              this.showUsages(ingredient, err);
            }
          },
        });
      });
  }

  private showUsages(ingredient: Ingredient, err: HttpErrorResponse): void {
    this.service.usages(ingredient.id).subscribe({
      next: (usages) => {
        const body = err.error as { message?: string } | null;
        const dialogData: UsagesDialogData = {
          message:
            body?.message ??
            `O ingredente "${ingredient.name}" está em uso e não pode ser excluído.`,
          usages: { preparations: usages.preparations ?? [], sheets: usages.sheets ?? [] },
        };
        this.dialog.open(IngredientUsagesDialog, {
          data: dialogData,
          width: 'min(560px, 96vw)',
        });
      },
      error: () => undefined,
    });
  }
}
