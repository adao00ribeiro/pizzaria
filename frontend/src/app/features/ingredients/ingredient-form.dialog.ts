import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';

import { UNITS, Unit } from '../../core/models/common.models';
import { UNIT_LABELS } from '../../core/models/labels';
import { Ingredient, IngredientRequest, IngredientUsages } from '../../core/models/ingredient.models';
import { MoneyInputDirective, formatBrl } from '../../shared/components/money-input';
import { NonNullableFormControl, NonNullableFormGroup } from '../../core/forms.compat';

export interface IngredientDialogData {
  ingredient?: Ingredient;
}

/** Diálogo de criação/edição de ingrediente com custo por unidade ao vivo (docs/08 §3.3). */
@Component({
  selector: 'pz-ingredient-form-dialog',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatIconModule,
    MoneyInputDirective,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ data.ingredient ? 'Editar ingrediente' : 'Novo ingrediente' }}</h2>

    <mat-dialog-content>
      <form [formGroup]="form" (ngSubmit)="save()" novalidate>
        @if (editing) {
          <div class="live-calc" style="margin-bottom: 12px; background: #fdecea; border-color: #f2a9a6;">
            <span>
              <mat-icon style="vertical-align: middle;">info</mat-icon>
              Alterações recalculam preparos, fichas e alertas.
            </span>
          </div>
        }

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Nome do ingrediente</mat-label>
          <input matInput formControlName="name" placeholder="Ex.: Muçarela" />
          @if (form.controls.name.hasError('required')) {
            <mat-error>Nome obrigatório</mat-error>
          } @else if (form.controls.name.hasError('minlength')) {
            <mat-error>Informe ao menos 2 caracteres</mat-error>
          }
        </mat-form-field>

        <div class="form-row-3">
          <mat-form-field appearance="outline">
            <mat-label>Unidade de compra</mat-label>
            <mat-select formControlName="purchaseUnit">
              @for (unit of units; track unit) {
                <mat-option [value]="unit">{{ unitLabels[unit] }}</mat-option>
              }
            </mat-select>
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Quantidade comprada</mat-label>
            <input matInput type="number" formControlName="purchaseQuantity" min="0" step="any" />
            <mat-hint>Na unidade acima (ex.: 5000 g)</mat-hint>
            @if (form.controls.purchaseQuantity.hasError('required')) {
              <mat-error>Quantidade obrigatória</mat-error>
            } @else if (form.controls.purchaseQuantity.hasError('min')) {
              <mat-error>Deve ser maior que zero</mat-error>
            }
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Preço de compra</mat-label>
            <input matInput pzMoney formControlName="purchasePrice" />
            @if (form.controls.purchasePrice.hasError('required')) {
              <mat-error>Informe o preço pago</mat-error>
            } @else if (form.controls.purchasePrice.hasError('min')) {
              <mat-error>Não pode ser negativo</mat-error>
            }
          </mat-form-field>
        </div>

        <div class="form-row">
          <mat-form-field appearance="outline">
            <mat-label>Perda / desperdício (%)</mat-label>
            <input matInput type="number" formControlName="lossPercentage" min="0" max="100" step="any" />
            <mat-hint>Percentual de 0 a 100</mat-hint>
            @if (form.controls.lossPercentage.hasError('min') || form.controls.lossPercentage.hasError('max')) {
              <mat-error>Informe um valor entre 0 e 100</mat-error>
            }
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Estoque mínimo</mat-label>
            <input matInput type="number" formControlName="minimumStock" min="0" step="any" />
            <mat-hint>Quantidade mínima para alerta de reposição</mat-hint>
          </mat-form-field>
        </div>

        <div class="live-calc">
          <span>Custo por unidade (cálculo ao vivo):</span>
          <strong>{{ formatBrl(unitCost()) }}</strong>
        </div>
        <p class="hint-text">
          Custo = preço de compra ÷ (quantidade − perda). O valor final é confirmado pelo servidor ao salvar.
        </p>
      </form>
    </mat-dialog-content>

    <mat-dialog-actions align="end">
      <button mat-button type="button" mat-dialog-close>Cancelar</button>
      <button mat-flat-button color="primary" type="button" (click)="save()">
        {{ editing ? 'Salvar alterações' : 'Cadastrar' }}
      </button>
    </mat-dialog-actions>
  `,
})
export class IngredientFormDialog {
  readonly data = inject<IngredientDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject<MatDialogRef<IngredientFormDialog, IngredientRequest>>(MatDialogRef);

  readonly editing = !!this.data.ingredient;
  readonly units: Unit[] = [...UNITS];
  readonly unitLabels = UNIT_LABELS;
  readonly formatBrl = formatBrl;

  readonly form = new NonNullableFormGroup({
    name: new NonNullableFormControl(this.data.ingredient?.name ?? '', [
      Validators.required,
      Validators.minLength(2),
    ]),
    purchaseUnit: new NonNullableFormControl<Unit>(this.data.ingredient?.purchaseUnit ?? 'g', [
      Validators.required,
    ]),
    purchaseQuantity: new NonNullableFormControl<number>(this.data.ingredient?.purchaseQuantity ?? 0, [
      Validators.required,
      Validators.min(0.0001),
    ]),
    purchasePrice: new NonNullableFormControl<number | null>(this.data.ingredient?.purchasePrice ?? 0, [
      Validators.required,
      Validators.min(0),
    ]),
    lossPercentage: new NonNullableFormControl<number>(this.data.ingredient?.lossPercentage ?? 0, [
      Validators.required,
      Validators.min(0),
      Validators.max(100),
    ]),
    minimumStock: new NonNullableFormControl<number>(this.data.ingredient?.minimumStock ?? 0, [
      Validators.required,
      Validators.min(0),
    ]),
  });

  readonly unitCost = signal<number | null>(this.computeUnitCost());

  constructor() {
    this.form.valueChanges.subscribe(() => this.unitCost.set(this.computeUnitCost()));
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const value = this.form.getRawValue();
    this.dialogRef.close({
      name: value.name.trim(),
      purchaseUnit: value.purchaseUnit,
      purchaseQuantity: Number(value.purchaseQuantity),
      purchasePrice: Number(value.purchasePrice ?? 0),
      lossPercentage: Number(value.lossPercentage),
      minimumStock: Number(value.minimumStock),
    });
  }

  private computeUnitCost(): number | null {
    const { purchasePrice, purchaseQuantity, lossPercentage } = this.form.getRawValue();
    const quantity = Number(purchaseQuantity);
    const price = Number(purchasePrice ?? 0);
    if (!quantity || quantity <= 0) {
      return null;
    }
    const usable = quantity * (1 - Number(lossPercentage ?? 0) / 100);
    if (usable <= 0) {
      return null;
    }
    return price / usable;
  }
}

export interface UsagesDialogData {
  message: string;
  usages: IngredientUsages;
}

/** Lista preparos e fichas que usam o ingrediente (conflito de exclusão 409). */
@Component({
  selector: 'pz-ingredient-usages-dialog',
  standalone: true,
  imports: [MatDialogModule, MatButtonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>Ingrediente em uso</h2>
    <mat-dialog-content>
      <p style="margin-top: 0;">{{ data.message }}</p>

      <p class="card-title">Preparos que usam este ingrediente ({{ data.usages.preparations.length }})</p>
      @if (data.usages.preparations.length === 0) {
        <p class="hint-text">Nenhum preparo utiliza este ingrediente.</p>
      } @else {
        <ul class="detail-list">
          @for (p of data.usages.preparations; track p.id) {
            <li>
              <span class="detail-label">{{ p.name }}</span>
            </li>
          }
        </ul>
      }

      <p class="card-title" style="margin-top: 16px;">
        Fichas técnicas que usam este ingrediente ({{ data.usages.sheets.length }})
      </p>
      @if (data.usages.sheets.length === 0) {
        <p class="hint-text">Nenhuma ficha técnica utiliza este ingrediente.</p>
      } @else {
        <ul class="detail-list">
          @for (s of data.usages.sheets; track s.id) {
            <li>
              <span class="detail-label">{{ s.productName }} — {{ s.sizeName }}</span>
            </li>
          }
        </ul>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-flat-button color="primary" mat-dialog-close>Entendi</button>
    </mat-dialog-actions>
  `,
})
export class IngredientUsagesDialog {
  readonly data = inject<UsagesDialogData>(MAT_DIALOG_DATA);
}
