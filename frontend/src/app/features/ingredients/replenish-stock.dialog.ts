import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { finalize } from 'rxjs';

import { Ingredient } from '../../core/models/ingredient.models';
import { StockMovementRequest } from '../../core/models/stock.models';
import { BrlPipe } from '../../shared/pipes/brl.pipe';
import { MoneyInputDirective } from '../../shared/components/money-input';
import { IngredientsService } from './ingredients.service';
import { StockService } from '../stock/stock.service';

export interface ReplenishStockDialogData {
  ingredient?: Ingredient;
  ingredientId?: string;
  ingredientName?: string;
}

/**
 * Diálogo rápido para dar entrada de compras e reposição de estoque de insumos.
 * Permite registrar a quantidade comprada (+), custo unitário opcional e observação.
 */
@Component({
  selector: 'pz-replenish-stock-dialog',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MoneyInputDirective,
    BrlPipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="replenish-dialog-header">
      <div class="title-with-icon">
        <mat-icon class="title-icon">add_shopping_cart</mat-icon>
        <h2 mat-dialog-title class="dialog-title">
          @if (selectedIngredient()) {
            Repor Estoque — {{ selectedIngredient()?.name }}
          } @else {
            Dar entrada / Repor Estoque
          }
        </h2>
      </div>
      <button mat-icon-button mat-dialog-close type="button" aria-label="Fechar">
        <mat-icon>close</mat-icon>
      </button>
    </div>

    <mat-dialog-content class="replenish-dialog-content">
      @if (loadingIngredients()) {
        <div class="dialog-loader">
          <mat-spinner diameter="36" />
          <span>Carregando ingredientes...</span>
        </div>
      } @else {
        <form [formGroup]="form" (ngSubmit)="save()" novalidate>
          <!-- Seletor de ingrediente quando não vier fixo ou se o usuário quiser trocar -->
          @if (!lockIngredientSelection) {
            <mat-form-field appearance="outline" class="full-width">
              <mat-label>Ingrediente a repor</mat-label>
              <mat-select
                formControlName="ingredientId"
                placeholder="Selecione o insumo comprado"
                (selectionChange)="onIngredientChange($event.value)"
              >
                @for (item of ingredients(); track item.id) {
                  <mat-option [value]="item.id">
                    {{ item.name }} (Estoque atual: {{ item.currentStock }} {{ item.purchaseUnit }})
                  </mat-option>
                }
              </mat-select>
              @if (form.controls.ingredientId.hasError('required')) {
                <mat-error>Selecione o ingrediente a repor</mat-error>
              }
            </mat-form-field>
          }

          @if (selectedIngredient(); as ing) {
            <!-- Card de Resumo de Estoque Atual -->
            <div class="stock-summary-card" [class.is-low]="isLowStock(ing)">
              <div class="summary-col">
                <span class="summary-label">Estoque Atual</span>
                <span class="summary-value" [class.danger-value]="isLowStock(ing)">
                  {{ ing.currentStock }} {{ ing.purchaseUnit }}
                </span>
                @if (ing.currentStock < 0) {
                  <span class="status-pill pill-danger">
                    <mat-icon>error_outline</mat-icon> Negativo (vendas sem entrada)
                  </span>
                } @else if (isLowStock(ing)) {
                  <span class="status-pill pill-warn">
                    <mat-icon>warning</mat-icon> Abaixo do mínimo
                  </span>
                } @else {
                  <span class="status-pill pill-ok">
                    <mat-icon>check_circle</mat-icon> Normal
                  </span>
                }
              </div>

              <div class="summary-col">
                <span class="summary-label">Estoque Mínimo</span>
                <span class="summary-value">{{ ing.minimumStock }} {{ ing.purchaseUnit }}</span>
                <span class="summary-sub">Ponto de reposição</span>
              </div>

              <div class="summary-col">
                <span class="summary-label">Custo Cadastral</span>
                <span class="summary-value">{{ ing.unitCost | brl }}</span>
                <span class="summary-sub">por {{ ing.purchaseUnit }}</span>
              </div>
            </div>

            <!-- Campo Principal: Quantidade Comprada a Adicionar -->
            <mat-form-field appearance="outline" class="full-width qty-field">
              <mat-label>Quantidade comprada a adicionar (+)</mat-label>
              <input
                matInput
                type="number"
                formControlName="quantity"
                min="0.0001"
                step="any"
                placeholder="Ex.: 5"
                autocomplete="off"
                autofocus
              />
              <span matTextSuffix class="unit-suffix">
                <strong>{{ ing.purchaseUnit }}</strong>
              </span>
              @if (form.controls.quantity.hasError('required')) {
                <mat-error>Informe a quantidade que foi comprada</mat-error>
              } @else if (form.controls.quantity.hasError('min')) {
                <mat-error>A quantidade deve ser maior que zero</mat-error>
              }
            </mat-form-field>

            <!-- Preview do novo saldo após reposição -->
            @if (projectedStock() !== null) {
              <div
                class="projection-banner"
                [class.projection-ok]="!projectedBelowMinimum()"
                [class.projection-warn]="projectedBelowMinimum()"
              >
                <mat-icon>{{ projectedBelowMinimum() ? 'warning_amber' : 'trending_up' }}</mat-icon>
                <div class="projection-text">
                  <span>Novo estoque após entrada:</span>
                  <strong>{{ projectedStock() }} {{ ing.purchaseUnit }}</strong>
                  @if (projectedBelowMinimum()) {
                    <span class="projection-sub">
                      (Ainda continuará abaixo do mínimo de {{ ing.minimumStock }} {{ ing.purchaseUnit }})
                    </span>
                  } @else {
                    <span class="projection-sub">(Estoque regularizado acima do mínimo!)</span>
                  }
                </div>
              </div>
            }

            <div class="form-row">
              <!-- Preço / Custo Unitário Opcional -->
              <mat-form-field appearance="outline">
                <mat-label>Preço unitário pago (R$ / {{ ing.purchaseUnit }})</mat-label>
                <input matInput pzMoney formControlName="unitCost" placeholder="Opcional" />
                <mat-hint>Opcional: atualiza o custo base do insumo</mat-hint>
                @if (form.controls.unitCost.hasError('min')) {
                  <mat-error>Não pode ser negativo</mat-error>
                }
              </mat-form-field>

              <!-- Observação da Compra -->
              <mat-form-field appearance="outline">
                <mat-label>Observação / Fornecedor</mat-label>
                <input
                  matInput
                  formControlName="note"
                  placeholder="Ex.: Compra Atacadão, NF 1204"
                  maxlength="200"
                />
                <mat-hint>Opcional: histórico de compras</mat-hint>
              </mat-form-field>
            </div>
          }
        </form>
      }
    </mat-dialog-content>

    <mat-dialog-actions align="end" class="dialog-actions">
      <button mat-button type="button" mat-dialog-close [disabled]="saving()">
        Cancelar
      </button>

      <button
        mat-flat-button
        color="primary"
        type="button"
        [disabled]="form.invalid || !selectedIngredient() || saving()"
        (click)="save()"
      >
        @if (saving()) {
          <mat-spinner diameter="20" />
          <span>Salvando...</span>
        } @else {
          <ng-container>
            <mat-icon>check</mat-icon>
            Confirmar Entrada (+)
          </ng-container>
        }
      </button>
    </mat-dialog-actions>
  `,
  styles: [
    `
      .replenish-dialog-header {
        display: flex;
        align-items: center;
        justify-content: space-between;
        padding: 16px 20px 8px;
        border-bottom: 1px solid rgba(0, 0, 0, 0.08);

        .title-with-icon {
          display: flex;
          align-items: center;
          gap: 10px;
        }

        .title-icon {
          color: #c62828;
          font-size: 26px;
          width: 26px;
          height: 26px;
        }

        .dialog-title {
          margin: 0;
          font-size: 1.25rem;
          font-weight: 600;
        }
      }

      .replenish-dialog-content {
        padding: 20px 20px 10px;
        min-width: min(520px, 92vw);
      }

      .dialog-loader {
        display: flex;
        flex-direction: column;
        align-items: center;
        gap: 12px;
        padding: 32px 0;
        color: rgba(0, 0, 0, 0.6);
      }

      .stock-summary-card {
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(130px, 1fr));
        gap: 12px;
        background: #f8f9fa;
        border: 1px solid #e9ecef;
        border-radius: 10px;
        padding: 14px 16px;
        margin-bottom: 18px;

        &.is-low {
          background: #fff5f5;
          border-color: #fcc2c2;
        }

        .summary-col {
          display: flex;
          flex-direction: column;
          gap: 4px;
        }

        .summary-label {
          font-size: 0.75rem;
          text-transform: uppercase;
          letter-spacing: 0.04em;
          color: rgba(0, 0, 0, 0.55);
          font-weight: 600;
        }

        .summary-value {
          font-size: 1.25rem;
          font-weight: 700;
          color: #212529;

          &.danger-value {
            color: #c62828;
          }
        }

        .summary-sub {
          font-size: 0.75rem;
          color: rgba(0, 0, 0, 0.5);
        }

        .status-pill {
          display: inline-flex;
          align-items: center;
          gap: 4px;
          font-size: 0.72rem;
          font-weight: 600;
          padding: 2px 8px;
          border-radius: 12px;
          width: fit-content;

          .mat-icon {
            font-size: 14px;
            width: 14px;
            height: 14px;
          }

          &.pill-danger {
            background: #ffebee;
            color: #c62828;
          }

          &.pill-warn {
            background: #fff8e1;
            color: #b26a00;
          }

          &.pill-ok {
            background: #e8f5e9;
            color: #2e7d32;
          }
        }
      }

      .qty-field {
        margin-bottom: 8px;

        input {
          font-size: 1.2rem;
          font-weight: 600;
          color: #1b5e20;
        }

        .unit-suffix {
          margin-right: 8px;
          color: rgba(0, 0, 0, 0.7);
          font-size: 1rem;
        }
      }

      .projection-banner {
        display: flex;
        align-items: center;
        gap: 10px;
        padding: 10px 14px;
        border-radius: 8px;
        margin-bottom: 16px;
        font-size: 0.875rem;

        .mat-icon {
          font-size: 22px;
          width: 22px;
          height: 22px;
          flex-shrink: 0;
        }

        .projection-text {
          display: flex;
          flex-wrap: wrap;
          align-items: center;
          gap: 6px;

          strong {
            font-size: 1rem;
          }

          .projection-sub {
            font-size: 0.8rem;
            opacity: 0.85;
          }
        }

        &.projection-ok {
          background: #e8f5e9;
          border: 1px solid #c8e6c9;
          color: #1b5e20;

          .mat-icon {
            color: #2e7d32;
          }
        }

        &.projection-warn {
          background: #fff8e1;
          border: 1px solid #ffe082;
          color: #b26a00;

          .mat-icon {
            color: #f57f17;
          }
        }
      }

      .dialog-actions {
        padding: 12px 20px 16px;
        gap: 10px;

        button {
          min-width: 110px;
          display: inline-flex;
          align-items: center;
          gap: 6px;
        }
      }

      :host-context(.dark-theme) {
        .replenish-dialog-header {
          border-bottom-color: rgba(255, 255, 255, 0.1);

          .title-icon {
            color: #f87171;
          }

          .dialog-title {
            color: var(--pz-text-main);
          }
        }

        .dialog-loader {
          color: var(--pz-text-muted);
        }

        .stock-summary-card {
          background: #1c202c !important;
          border-color: rgba(255, 255, 255, 0.1) !important;

          &.is-low {
            background: rgba(248, 113, 113, 0.12) !important;
            border-color: rgba(248, 113, 113, 0.3) !important;
          }

          .summary-label {
            color: #94a3b8 !important;
          }

          .summary-value {
            color: #f8fafc !important;

            &.danger-value {
              color: #f87171 !important;
            }
          }

          .summary-sub {
            color: #64748b !important;
          }

          .status-pill.pill-danger {
            background: rgba(248, 113, 113, 0.2) !important;
            color: #fca5a5 !important;
          }

          .status-pill.pill-warn {
            background: rgba(251, 191, 36, 0.2) !important;
            color: #fde68a !important;
          }

          .status-pill.pill-ok {
            background: rgba(52, 211, 153, 0.2) !important;
            color: #6ee7b7 !important;
          }
        }

        .qty-field {
          input {
            color: #34d399 !important;
          }

          .unit-suffix {
            color: #94a3b8 !important;
          }
        }

        .projection-banner {
          &.projection-ok {
            background: rgba(52, 211, 153, 0.12) !important;
            border-color: rgba(52, 211, 153, 0.3) !important;
            color: #34d399 !important;

            .mat-icon {
              color: #34d399 !important;
            }
          }

          &.projection-warn {
            background: rgba(251, 191, 36, 0.12) !important;
            border-color: rgba(251, 191, 36, 0.3) !important;
            color: #fbbf24 !important;

            .mat-icon {
              color: #fbbf24 !important;
            }
          }
        }
      }
    `,
  ],
})
export class ReplenishStockDialog implements OnInit {
  private readonly dialogRef = inject(MatDialogRef<ReplenishStockDialog, boolean>);
  readonly data = inject<ReplenishStockDialogData>(MAT_DIALOG_DATA, { optional: true });
  private readonly ingredientsService = inject(IngredientsService);
  private readonly stockService = inject(StockService);
  private readonly snack = inject(MatSnackBar);
  private readonly fb = inject(FormBuilder);

  readonly lockIngredientSelection = !!this.data?.ingredient;

  readonly ingredients = signal<Ingredient[]>([]);
  readonly selectedIngredient = signal<Ingredient | null>(this.data?.ingredient ?? null);
  readonly loadingIngredients = signal(false);
  readonly saving = signal(false);

  readonly form = this.fb.group({
    ingredientId: [this.data?.ingredient?.id ?? '', [Validators.required]],
    quantity: [null as number | null, [Validators.required, Validators.min(0.0001)]],
    unitCost: [null as number | null, [Validators.min(0)]],
    note: [''],
  });

  readonly projectedStock = computed(() => {
    const ing = this.selectedIngredient();
    const qty = this.form.controls.quantity.value;
    if (!ing || qty === null || qty === undefined || isNaN(qty) || qty <= 0) {
      return null;
    }
    // Arredonda para 3 casas decimais
    return Math.round((ing.currentStock + Number(qty)) * 1000) / 1000;
  });

  readonly projectedBelowMinimum = computed(() => {
    const ing = this.selectedIngredient();
    const projected = this.projectedStock();
    if (!ing || projected === null) {
      return false;
    }
    return projected < ing.minimumStock;
  });

  ngOnInit(): void {
    if (this.data?.ingredient) {
      this.selectedIngredient.set(this.data.ingredient);
      this.form.controls.ingredientId.setValue(this.data.ingredient.id);
      return;
    }

    this.loadAllIngredients();
  }

  private loadAllIngredients(): void {
    this.loadingIngredients.set(true);
    this.ingredientsService
      .list({ page: 1, pageSize: 200 })
      .pipe(finalize(() => this.loadingIngredients.set(false)))
      .subscribe({
        next: (res) => {
          this.ingredients.set(res.items);

          // Se veio ingredientId especificado
          if (this.data?.ingredientId) {
            const found = res.items.find((i) => i.id === this.data?.ingredientId);
            if (found) {
              this.onIngredientChange(found.id);
              return;
            }
          }

          // Se veio ingredientName especificado (ex: extraído da mensagem de alerta)
          if (this.data?.ingredientName) {
            const normalizedName = this.data.ingredientName.trim().toLowerCase();
            const found = res.items.find(
              (i) => i.name.trim().toLowerCase() === normalizedName,
            );
            if (found) {
              this.onIngredientChange(found.id);
              return;
            }
          }
        },
        error: () => undefined,
      });
  }

  onIngredientChange(ingredientId: string): void {
    this.form.controls.ingredientId.setValue(ingredientId);
    const item =
      this.ingredients().find((i) => i.id === ingredientId) ??
      (this.data?.ingredient?.id === ingredientId ? this.data.ingredient : null);

    this.selectedIngredient.set(item ?? null);
  }

  isLowStock(ingredient: Ingredient): boolean {
    return ingredient.currentStock < ingredient.minimumStock;
  }

  save(): void {
    if (this.form.invalid || !this.selectedIngredient() || this.saving()) {
      return;
    }

    const val = this.form.getRawValue();
    const ingredient = this.selectedIngredient()!;
    const quantity = Number(val.quantity);

    const body: StockMovementRequest = {
      ingredientId: ingredient.id,
      type: 'In',
      quantity,
      unitCost: val.unitCost !== null && val.unitCost !== undefined && val.unitCost !== ('' as unknown)
        ? Number(val.unitCost)
        : null,
      note: val.note?.trim() || null,
    };

    this.saving.set(true);
    this.stockService
      .createMovement(body)
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: () => {
          this.snack.open(
            `Estoque de "${ingredient.name}" atualizado com sucesso! (+${quantity} ${ingredient.purchaseUnit})`,
            'Fechar',
            { duration: 4000, panelClass: 'success-snack' },
          );
          this.dialogRef.close(true);
        },
        error: () => undefined,
      });
  }
}
