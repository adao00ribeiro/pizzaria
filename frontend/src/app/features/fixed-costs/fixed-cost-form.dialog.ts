import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import {
  FormControl,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatRadioModule } from '@angular/material/radio';

import { AllocationType } from '../../core/models/common.models';
import { ALLOCATION_LABELS } from '../../core/models/labels';
import { FixedCost, FixedCostRequest } from '../../core/models/cost.models';
import { MoneyInputDirective } from '../../shared/components/money-input';
import { NonNullableFormControl, NonNullableFormGroup } from '../../core/forms.compat';

export interface FixedCostDialogData {
  fixedCost?: FixedCost;
}

/** Diálogo de criação/edição de custo fixo com rateio (por pizza ou % do faturamento). */
@Component({
  selector: 'pz-fixed-cost-form-dialog',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatRadioModule,
    MoneyInputDirective,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ data.fixedCost ? 'Editar custo fixo' : 'Novo custo fixo' }}</h2>

    <mat-dialog-content>
      <form [formGroup]="form" novalidate>
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Descrição</mat-label>
          <input matInput formControlName="description" placeholder="Ex.: Aluguel do salão" />
          @if (form.controls.description.hasError('required')) {
            <mat-error>Descrição obrigatória</mat-error>
          }
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Valor mensal</mat-label>
          <input matInput pzMoney formControlName="monthlyValue" />
          @if (form.controls.monthlyValue.hasError('required')) {
            <mat-error>Informe o valor mensal</mat-error>
          } @else if (form.controls.monthlyValue.hasError('min')) {
            <mat-error>Não pode ser negativo</mat-error>
          }
        </mat-form-field>

        <p class="card-title">Como ratear por pizza?</p>
        <mat-radio-group formControlName="allocationType" class="allocation-group">
          @for (type of allocationTypes; track type) {
            <mat-radio-button [value]="type">{{ allocationLabels[type] }}</mat-radio-button>
          }
        </mat-radio-group>

        @if (form.controls.allocationType.value === 'PerPizza') {
          <mat-form-field appearance="outline" class="full-width">
            <mat-label>Pizzas vendidas por mês (estimativa)</mat-label>
            <input matInput type="number" formControlName="expectedPizzasPerMonth" min="1" step="1" />
            @if (form.controls.expectedPizzasPerMonth.hasError('required')) {
              <mat-error>Informe a quantidade estimada de pizzas</mat-error>
            } @else if (form.controls.expectedPizzasPerMonth.hasError('min')) {
              <mat-error>Deve ser maior que zero</mat-error>
            }
          </mat-form-field>
        } @else {
          <mat-form-field appearance="outline" class="full-width">
            <mat-label>Faturamento mensal estimado</mat-label>
            <input matInput pzMoney formControlName="expectedMonthlyRevenue" />
            @if (form.controls.expectedMonthlyRevenue.hasError('required')) {
              <mat-error>Informe o faturamento mensal estimado</mat-error>
            } @else if (form.controls.expectedMonthlyRevenue.hasError('min')) {
              <mat-error>Deve ser maior que zero</mat-error>
            }
          </mat-form-field>
        }

        <p class="hint-text">
          O rateio entra no custo indireto de cada pizza quando a ficha técnica é calculada.
        </p>
      </form>
    </mat-dialog-content>

    <mat-dialog-actions align="end">
      <button mat-button type="button" mat-dialog-close>Cancelar</button>
      <button mat-flat-button color="primary" type="button" (click)="save()">
        {{ data.fixedCost ? 'Salvar alterações' : 'Cadastrar' }}
      </button>
    </mat-dialog-actions>
  `,
  styles: [
    `
      .allocation-group {
        display: flex;
        flex-direction: column;
        gap: 8px;
        margin-bottom: 16px;
      }
    `,
  ],
})
export class FixedCostFormDialog {
  readonly data = inject<FixedCostDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject<MatDialogRef<FixedCostFormDialog, FixedCostRequest>>(MatDialogRef);

  readonly allocationTypes: AllocationType[] = ['PerPizza', 'RevenuePercentage'];
  readonly allocationLabels = ALLOCATION_LABELS;

  readonly form = new NonNullableFormGroup({
    description: new NonNullableFormControl(this.data.fixedCost?.description ?? '', [
      Validators.required,
      Validators.minLength(2),
    ]),
    monthlyValue: new FormControl<number | null>(this.data.fixedCost?.monthlyValue ?? 0, [
      Validators.required,
      Validators.min(0),
    ]),
    allocationType: new NonNullableFormControl<AllocationType>(
      this.data.fixedCost?.allocationType ?? 'PerPizza',
      [Validators.required],
    ),
    expectedPizzasPerMonth: new FormControl<number | null>(
      this.data.fixedCost?.expectedPizzasPerMonth ?? null,
      [Validators.min(1)],
    ),
    expectedMonthlyRevenue: new FormControl<number | null>(
      this.data.fixedCost?.expectedMonthlyRevenue ?? null,
      [Validators.min(0.01)],
    ),
  });

  constructor() {
    this.applyConditionalValidators();
    this.form.controls.allocationType.valueChanges.subscribe(() => this.applyConditionalValidators());
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const value = this.form.getRawValue();
    const isPerPizza = value.allocationType === 'PerPizza';
    this.dialogRef.close({
      description: value.description.trim(),
      monthlyValue: Number(value.monthlyValue ?? 0),
      allocationType: value.allocationType,
      expectedPizzasPerMonth: isPerPizza ? Number(value.expectedPizzasPerMonth ?? 0) : null,
      expectedMonthlyRevenue: isPerPizza ? null : Number(value.expectedMonthlyRevenue ?? 0),
    });
  }

  private applyConditionalValidators(): void {
    const isPerPizza = this.form.controls.allocationType.value === 'PerPizza';
    const pizzas = this.form.controls.expectedPizzasPerMonth;
    const revenue = this.form.controls.expectedMonthlyRevenue;

    pizzas.setValidators(isPerPizza ? [Validators.required, Validators.min(1)] : [Validators.min(1)]);
    revenue.setValidators(
      isPerPizza ? [Validators.min(0.01)] : [Validators.required, Validators.min(0.01)],
    );
    pizzas.updateValueAndValidity({ emitEvent: false });
    revenue.updateValueAndValidity({ emitEvent: false });
  }
}
