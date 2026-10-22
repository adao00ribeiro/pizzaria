import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTooltipModule } from '@angular/material/tooltip';
import { finalize } from 'rxjs';

import { PAGE_SIZE, StockMovementType, Unit } from '../../core/models/common.models';
import { STOCK_TYPE_LABELS, UNIT_LABELS } from '../../core/models/labels';
import { Ingredient } from '../../core/models/ingredient.models';
import { StockBalance, StockMovement, StockMovementRequest } from '../../core/models/stock.models';
import { MoneyInputDirective } from '../../shared/components/money-input';
import { BrlPipe } from '../../shared/pipes/brl.pipe';
import { IngredientsService } from '../ingredients/ingredients.service';
import { StockService } from './stock.service';

/**
 * Estoque — docs/08 §3.9.
 * Abas Saldos (com chip "abaixo do mínimo") e Movimentos (registro + histórico).
 */
@Component({
  selector: 'pz-stock-page',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatTabsModule,
    MatTableModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatChipsModule,
    MatPaginatorModule,
    MatProgressSpinnerModule,
    MatSlideToggleModule,
    MatTooltipModule,
    MatSnackBarModule,
    MoneyInputDirective,
    DatePipe,
    BrlPipe,
  ],
  templateUrl: './stock.page.html',
  styleUrl: './stock.page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StockPage {
  private readonly service = inject(StockService);
  private readonly ingredientsService = inject(IngredientsService);
  private readonly snack = inject(MatSnackBar);
  private readonly route = inject(ActivatedRoute);
  private readonly fb = inject(NonNullableFormBuilder);

  readonly pageSize = PAGE_SIZE;
  readonly unitLabels = UNIT_LABELS;
  readonly movementTypeLabels = STOCK_TYPE_LABELS;
  readonly movementTypes: StockMovementType[] = ['In', 'Out', 'Adjustment'];

  movementTypeLabel(type: StockMovementType): string {
    return STOCK_TYPE_LABELS[type];
  }

  readonly balanceColumns = ['name', 'currentStock', 'minimumStock', 'status'];
  readonly movementColumns = ['createdAt', 'ingredientName', 'type', 'quantity', 'unitCost', 'note'];

  readonly activeTab = signal(0);

  // ----- saldos -----
  readonly balancesLoading = signal(true);
  readonly balances = signal<StockBalance[]>([]);
  readonly balancesTotal = signal(0);
  readonly balancesPage = signal(1);
  readonly belowMinimumOnly = signal(false);

  // ----- movimentos -----
  readonly movementsLoading = signal(true);
  readonly movements = signal<StockMovement[]>([]);
  readonly movementsTotal = signal(0);
  readonly movementsPage = signal(1);
  readonly registering = signal(false);
  readonly ingredients = signal<Ingredient[]>([]);

  readonly movementForm = this.fb.group({
    ingredientId: ['', [Validators.required]],
    type: ['Out' as StockMovementType, [Validators.required]],
    quantity: [0, [Validators.required, Validators.min(0.0001)]],
    unitCost: [0 as number | null, [Validators.min(0)]],
    note: [''],
  });

  constructor() {
    if (this.route.snapshot.queryParamMap.get('tab') === 'movements') {
      this.activeTab.set(1);
    }
    this.loadBalances();
    this.loadMovements();
    this.ingredientsService.list({ page: 1, pageSize: 200 }).subscribe({
      next: (res) => this.ingredients.set(res.items),
      error: () => undefined,
    });
  }

  onTab(index: number): void {
    this.activeTab.set(index);
  }

  loadBalances(): void {
    this.balancesLoading.set(true);
    this.service
      .balances({ page: this.balancesPage(), belowMinimum: this.belowMinimumOnly() })
      .pipe(finalize(() => this.balancesLoading.set(false)))
      .subscribe({
        next: (res) => {
          this.balances.set(res.items);
          this.balancesTotal.set(res.total);
        },
        error: () => undefined,
      });
  }

  onBalancesPage(event: PageEvent): void {
    this.balancesPage.set(event.pageIndex + 1);
    this.loadBalances();
  }

  toggleBelowMinimum(enabled: boolean): void {
    this.belowMinimumOnly.set(enabled);
    this.balancesPage.set(1);
    this.loadBalances();
  }

  loadMovements(): void {
    this.movementsLoading.set(true);
    this.service
      .movements({ page: this.movementsPage() })
      .pipe(finalize(() => this.movementsLoading.set(false)))
      .subscribe({
        next: (res) => {
          this.movements.set(res.items);
          this.movementsTotal.set(res.total);
        },
        error: () => undefined,
      });
  }

  onMovementsPage(event: PageEvent): void {
    this.movementsPage.set(event.pageIndex + 1);
    this.loadMovements();
  }

  registerMovement(): void {
    if (this.movementForm.invalid) {
      this.movementForm.markAllAsTouched();
      return;
    }
    const { ingredientId, type, quantity, unitCost, note } = this.movementForm.getRawValue();
    const body: StockMovementRequest = {
      ingredientId,
      type,
      quantity: Number(quantity),
      unitCost: unitCost === null || Number.isNaN(Number(unitCost)) ? null : Number(unitCost),
      note: note?.trim() ? note.trim() : null,
    };
    this.registering.set(true);
    this.service
      .createMovement(body)
      .pipe(finalize(() => this.registering.set(false)))
      .subscribe({
        next: () => {
          const label = STOCK_TYPE_LABELS[type];
          this.snack.open(`Movimento registrado (${label}).`, 'Fechar', {
            duration: 3000,
            panelClass: 'success-snack',
          });
          this.movementForm.patchValue({ quantity: 0, unitCost: 0, note: '' });
          this.loadMovements();
          this.loadBalances();
        },
        error: () => undefined,
      });
  }

  unitOf(ingredientId: string): Unit {
    return this.ingredients().find((i) => i.id === ingredientId)?.purchaseUnit ?? 'un';
  }
}
