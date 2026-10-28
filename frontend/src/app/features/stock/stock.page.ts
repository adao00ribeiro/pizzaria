import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { DatePipe } from '@angular/common';
import { FormControl, NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
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
import { combineLatest, debounceTime, distinctUntilChanged, finalize, startWith } from 'rxjs';

import { PAGE_SIZE, StockMovementType, Unit } from '../../core/models/common.models';
import { STOCK_REPLENISHMENT_URGENCY_LABELS, STOCK_TYPE_LABELS, UNIT_LABELS } from '../../core/models/labels';
import { Ingredient } from '../../core/models/ingredient.models';
import {
  StockBalance,
  StockMovement,
  StockMovementRequest,
  StockReplenishmentFilter,
  StockReplenishmentItem,
  StockReplenishmentReport,
  StockReplenishmentUrgency,
} from '../../core/models/stock.models';
import { TenantService } from '../../core/services/tenant.service';
import { MoneyInputDirective } from '../../shared/components/money-input';
import { BrlPipe } from '../../shared/pipes/brl.pipe';
import { IngredientsService } from '../ingredients/ingredients.service';
import { ReplenishStockDialog, ReplenishStockDialogData } from '../ingredients/replenish-stock.dialog';
import { StockService } from './stock.service';

/**
 * Estoque — docs/08 §3.9.
 * Abas:
 * 1. Saldos (com chip "abaixo do mínimo")
 * 2. Movimentos (registro + histórico)
 * 3. Lista de Compras / Reposição (KPIs, filtros, cálculo inteligente e impressão de compras)
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
    MatCheckboxModule,
    MatDialogModule,
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
  private readonly dialog = inject(MatDialog);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly fb = inject(NonNullableFormBuilder);
  readonly tenant = inject(TenantService);

  readonly pageSize = PAGE_SIZE;
  readonly unitLabels = UNIT_LABELS;
  readonly movementTypeLabels = STOCK_TYPE_LABELS;
  readonly urgencyLabels = STOCK_REPLENISHMENT_URGENCY_LABELS;
  readonly movementTypes: StockMovementType[] = ['In', 'Out', 'Adjustment'];

  movementTypeLabel(type: StockMovementType): string {
    return STOCK_TYPE_LABELS[type];
  }

  formatUnit(unit: Unit | string): string {
    return this.unitLabels[unit as Unit] ?? unit;
  }

  readonly balanceColumns = ['name', 'currentStock', 'minimumStock', 'status'];
  readonly movementColumns = ['createdAt', 'ingredientName', 'type', 'quantity', 'unitCost', 'note'];
  readonly replenishmentColumns = [
    'check',
    'ingredientName',
    'currentStock',
    'minimumStock',
    'deficitQuantity',
    'suggestedPurchase',
    'estimatedCost',
    'urgency',
    'actions',
  ];

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

  // ----- lista de compras / reposição -----
  readonly replenishmentLoading = signal(true);
  readonly replenishmentReport = signal<StockReplenishmentReport | null>(null);
  readonly checkedItemIds = signal<Set<string>>(new Set());

  readonly replenishmentSearch = new FormControl<string>('', { nonNullable: true });
  readonly replenishmentUrgency = new FormControl<StockReplenishmentUrgency | ''>('', { nonNullable: true });
  readonly replenishmentOnlyNeeded = new FormControl<boolean>(true, { nonNullable: true });

  constructor() {
    this.route.queryParamMap.pipe(takeUntilDestroyed()).subscribe((params) => {
      const tab = params.get('tab');
      if (tab === 'replenishment' || tab === 'compras') {
        this.activeTab.set(2);
      } else if (tab === 'movements') {
        this.activeTab.set(1);
      } else if (tab === 'balances') {
        this.activeTab.set(0);
      }
    });

    this.loadBalances();
    this.loadMovements();
    this.loadReplenishmentReport();

    // Reatividade dos filtros do relatório de compras
    combineLatest([
      this.replenishmentSearch.valueChanges.pipe(startWith(this.replenishmentSearch.value), debounceTime(250), distinctUntilChanged()),
      this.replenishmentUrgency.valueChanges.pipe(startWith(this.replenishmentUrgency.value), distinctUntilChanged()),
      this.replenishmentOnlyNeeded.valueChanges.pipe(startWith(this.replenishmentOnlyNeeded.value), distinctUntilChanged()),
    ])
      .pipe(takeUntilDestroyed())
      .subscribe(() => {
        this.loadReplenishmentReport();
      });

    this.ingredientsService.list({ page: 1, pageSize: 200 }).subscribe({
      next: (res) => this.ingredients.set(res.items),
      error: () => undefined,
    });
  }

  onTab(index: number): void {
    this.activeTab.set(index);
    const tabName = index === 2 ? 'replenishment' : index === 1 ? 'movements' : 'balances';
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { tab: tabName },
      queryParamsHandling: 'merge',
    });
    if (index === 2 && !this.replenishmentReport()) {
      this.loadReplenishmentReport();
    }
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
          this.loadReplenishmentReport();
        },
        error: () => undefined,
      });
  }

  unitOf(ingredientId: string): Unit {
    return this.ingredients().find((i) => i.id === ingredientId)?.purchaseUnit ?? 'un';
  }

  // ----- Métodos de Reposição & Compras -----

  loadReplenishmentReport(): void {
    this.replenishmentLoading.set(true);
    const filter: StockReplenishmentFilter = {
      search: this.replenishmentSearch.value.trim() || undefined,
      urgency: this.replenishmentUrgency.value || undefined,
      onlyNeedingReplenishment: this.replenishmentOnlyNeeded.value,
    };

    this.service
      .getReplenishmentReport(filter)
      .pipe(finalize(() => this.replenishmentLoading.set(false)))
      .subscribe({
        next: (report) => this.replenishmentReport.set(report),
        error: () => undefined,
      });
  }

  toggleCheckItem(id: string): void {
    this.checkedItemIds.update((set) => {
      const next = new Set(set);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });
  }

  isItemChecked(id: string): boolean {
    return this.checkedItemIds().has(id);
  }

  openReplenish(item: StockReplenishmentItem): void {
    this.dialog
      .open<ReplenishStockDialog, ReplenishStockDialogData, boolean>(ReplenishStockDialog, {
        data: { ingredientId: item.ingredientId, ingredientName: item.ingredientName },
        width: 'min(620px, 96vw)',
        autoFocus: 'first',
      })
      .afterClosed()
      .subscribe((saved) => {
        if (saved) {
          this.loadReplenishmentReport();
          this.loadBalances();
          this.loadMovements();
        }
      });
  }

  printReport(): void {
    window.print();
  }
}
