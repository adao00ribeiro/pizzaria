import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormArray, FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';
import {
  catchError,
  debounceTime,
  distinctUntilChanged,
  of,
  startWith,
  switchMap,
} from 'rxjs';

import { extractApiErrorMessage } from '../../core/interceptors/error.interceptor';
import { Combo, ComboPreviewResult } from '../../core/models/pricing.models';
import { Product, Size } from '../../core/models/product.models';
import { MarginStatusChip } from '../../shared/components/margin-status-chip';
import { BrlPipe } from '../../shared/pipes/brl.pipe';
import { PctPipe } from '../../shared/pipes/pct.pipe';
import { ProductsService } from '../products/products.service';
import { PricingService } from './pricing.service';
import { ComboRequest } from './combos.service';

/** Dados do diálogo de combo. */
export interface ComboFormDialogData {
  combo?: Combo;
  sizes: Size[];
}

/**
 * Montador de combo — docs/08 §3.8.
 * Itens (pizza sabor+tamanho ou bebida) + desconto + preço fixo opcional,
 * com preview de margem ao vivo via `POST /pricing/combos/preview`.
 */
@Component({
  selector: 'pz-combo-form-dialog',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatIconModule,
    MatTooltipModule,
    MarginStatusChip,
    BrlPipe,
    PctPipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ data.combo ? 'Editar combo' : 'Novo combo' }}</h2>
    <mat-dialog-content>
      <form [formGroup]="form" class="dialog-form">
        <mat-form-field appearance="outline">
          <mat-label>Nome do combo</mat-label>
          <input matInput formControlName="name" maxlength="120" />
          @if (form.controls.name.hasError('required')) {
            <mat-error>Informe o nome do combo.</mat-error>
          }
        </mat-form-field>

        <div class="form-row">
          <mat-form-field appearance="outline">
            <mat-label>Desconto (R$)</mat-label>
            <input matInput type="number" min="0" step="0.01" formControlName="discountValue" />
            @if (form.controls.discountValue.hasError('min')) {
              <mat-error>Não pode ser negativo.</mat-error>
            }
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Preço fixo (R$)</mat-label>
            <input matInput type="number" min="0" step="0.01" formControlName="fixedPrice" />
            <mat-hint>Opcional: sobrepõe o cálculo com desconto</mat-hint>
          </mat-form-field>
        </div>

        <h3 class="card-title">Itens do combo</h3>

        <div formArrayName="items">
          @for (item of items.controls; track $index; let i = $index) {
            <div class="item-row" [formGroupName]="i">
              <mat-form-field appearance="outline" class="item-autocomplete">
                <mat-label>Produto</mat-label>
                <mat-select formControlName="productId" (selectionChange)="onProductChange(i)">
                  @for (p of products(); track p.id) {
                    <mat-option [value]="p.id">{{ p.name }}</mat-option>
                  }
                </mat-select>
                @if (item.get('productId')?.hasError('required')) {
                  <mat-error>Selecione o produto.</mat-error>
                }
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>Tamanho</mat-label>
                <mat-select formControlName="sizeId">
                  <mat-option [value]="null">Sem tamanho (bebida)</mat-option>
                  @for (s of data.sizes; track s.id) {
                    <mat-option [value]="s.id">{{ s.name }}</mat-option>
                  }
                </mat-select>
              </mat-form-field>

              <mat-form-field appearance="outline" class="item-qty">
                <mat-label>Quantidade</mat-label>
                <input matInput type="number" min="1" step="1" formControlName="quantity" />
                @if (item.get('quantity')?.hasError('min')) {
                  <mat-error>Mínimo 1.</mat-error>
                }
              </mat-form-field>

              <button mat-icon-button type="button" matTooltip="Remover item" aria-label="Remover item" (click)="removeItem(i)">
                <mat-icon>delete</mat-icon>
              </button>
            </div>
          } @empty {
            <p class="hint-text">Adicione ao menos um item ao combo.</p>
          }
        </div>

        <div class="form-actions">
          <button mat-stroked-button type="button" (click)="addItem()">
            <mat-icon>add</mat-icon>
            Adicionar item
          </button>
        </div>

        @if (previewError()) {
          <p class="error-text">{{ previewError() }}</p>
        } @else if (preview()) {
          <div class="live-calc">
            <span>Custo: <strong>{{ preview()!.totalCost | brl }}</strong></span>
            <span>Preço: <strong>{{ preview()!.totalPrice | brl }}</strong></span>
            <span>Lucro: <strong>{{ preview()!.profit | brl }}</strong></span>
            <span>Margem: <strong>{{ preview()!.marginPercentage | pct }}</strong></span>
            <pz-margin-status-chip [status]="preview()!.status" />
          </div>
        }
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button type="button" mat-dialog-close>Cancelar</button>
      <button
        mat-flat-button
        color="primary"
        type="button"
        (click)="submit()"
        [disabled]="form.invalid || items.length === 0"
      >
        Salvar combo
      </button>
    </mat-dialog-actions>
  `,
})
export class ComboFormDialog {
  readonly data = inject<ComboFormDialogData>(MAT_DIALOG_DATA);
  private readonly ref = inject<MatDialogRef<ComboFormDialog, ComboRequest>>(MatDialogRef);
  private readonly fb = inject(FormBuilder);
  private readonly pricing = inject(PricingService);
  private readonly productsService = inject(ProductsService);

  readonly products = signal<Product[]>([]);
  readonly preview = signal<ComboPreviewResult | null>(null);
  readonly previewError = signal('');

  readonly form = this.fb.group({
    name: [this.data.combo?.name ?? '', [Validators.required, Validators.maxLength(120)]],
    discountValue: [this.data.combo?.discountValue ?? 0, [Validators.required, Validators.min(0)]],
    fixedPrice: [this.data.combo?.fixedPrice ?? null, [Validators.min(0)]],
    items: this.fb.array(
      (this.data.combo?.items ?? []).map((item) =>
        this.fb.group({
          productId: [item.productId, [Validators.required]],
          sizeId: [item.sizeId ?? null],
          quantity: [item.quantity, [Validators.required, Validators.min(1)]],
        }),
      ),
    ),
  });

  get items(): FormArray {
    return this.form.controls.items;
  }

  constructor() {
    this.productsService.products().subscribe({
      next: (items: Product[]) => this.products.set(items),
      error: () => undefined,
    });

    this.form.valueChanges
      .pipe(
        startWith(null),
        debounceTime(300),
        distinctUntilChanged((a, b) => JSON.stringify(a) === JSON.stringify(b)),
        switchMap(() => {
          const body = this.buildRequest();
          if (!body || body.items.length === 0) {
            this.previewError.set('');
            return of(null);
          }
          return this.pricing.comboPreview(body).pipe(
            catchError((err: HttpErrorResponse) => {
              this.previewError.set(extractApiErrorMessage(err));
              return of(null);
            }),
          );
        }),
      )
      .subscribe((res) => {
        if (res) {
          this.previewError.set('');
          this.preview.set(res);
        }
      });
  }

  addItem(): void {
    this.items.push(
      this.fb.group({
        productId: ['', [Validators.required]],
        sizeId: [null],
        quantity: [1, [Validators.required, Validators.min(1)]],
      }),
    );
  }

  removeItem(index: number): void {
    this.items.removeAt(index);
  }

  /** Produtos de bebida não usam tamanho de pizza. */
  onProductChange(index: number): void {
    const productId = this.items.at(index).get('productId')?.value as string;
    const product = this.products().find((p) => p.id === productId);
    if (product?.category === 'Beverage') {
      this.items.at(index).patchValue({ sizeId: null });
    } else if (!this.items.at(index).get('sizeId')?.value) {
      this.items.at(index).patchValue({ sizeId: this.data.sizes[0]?.id ?? null });
    }
  }

  submit(): void {
    if (this.form.invalid || this.items.length === 0) {
      this.form.markAllAsTouched();
      return;
    }
    this.ref.close(this.buildRequest());
  }

  private buildRequest(): ComboRequest {
    const { name, discountValue, fixedPrice } = this.form.getRawValue();
    const items = this.items.getRawValue().map((it) => ({
      productId: it.productId,
      sizeId: it.sizeId ?? undefined,
      quantity: Number(it.quantity),
    }));
    return {
      name: name ?? '',
      discountValue: Number(discountValue ?? 0),
      fixedPrice:
        fixedPrice === null || fixedPrice === undefined || Number.isNaN(Number(fixedPrice))
          ? null
          : Number(fixedPrice),
      items,
    };
  }
}
