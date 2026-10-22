import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';

import { Product, ProductRequest } from '../../core/models/product.models';
import { BrlPipe } from '../../shared/pipes/brl.pipe';

/** Dados do diálogo de produto. */
export interface ProductFormDialogData {
  product?: Product;
}

/**
 * Diálogo de criação/edição de produto (docs/07 §5).
 * Resolve com o body pronto para `POST/PUT /products`.
 */
@Component({
  selector: 'pz-product-form-dialog',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatIconModule,
    MatSlideToggleModule,
    BrlPipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: [
    `
      .image-preview-card {
        display: flex;
        align-items: center;
        gap: 14px;
        padding: 10px 14px;
        background: #f8fafc;
        border: 1px solid rgba(0, 0, 0, 0.08);
        border-radius: 10px;
        margin-bottom: 16px;
      }

      .image-preview-thumb {
        position: relative;
        width: 64px;
        height: 64px;
        border-radius: 8px;
        overflow: hidden;
        background: #f1f5f9;
        display: flex;
        align-items: center;
        justify-content: center;
        flex-shrink: 0;
        border: 1px solid rgba(0, 0, 0, 0.1);
      }

      .preview-fallback-icon {
        font-size: 30px;
        width: 30px;
        height: 30px;
        color: #94a3b8;
      }

      .preview-img {
        position: absolute;
        inset: 0;
        width: 100%;
        height: 100%;
        object-fit: cover;
      }

      .image-preview-status {
        display: flex;
        flex-direction: column;
        gap: 2px;
        font-size: 0.8125rem;
      }

      .preview-status-ok {
        display: flex;
        align-items: center;
        gap: 6px;
        color: #166534;
        font-weight: 500;
      }

      .preview-status-ok mat-icon {
        font-size: 16px;
        width: 16px;
        height: 16px;
      }

      .preview-status-error {
        display: flex;
        align-items: center;
        gap: 6px;
        color: #b91c1c;
        font-weight: 500;
      }

      .preview-status-error mat-icon {
        font-size: 16px;
        width: 16px;
        height: 16px;
      }

      .preview-hint {
        color: #64748b;
        font-size: 0.75rem;
      }

      :host-context(.dark-theme) {
        .image-preview-card {
          background: #1c202c !important;
          border-color: rgba(255, 255, 255, 0.1) !important;
        }

        .image-preview-thumb {
          background: #242938 !important;
          border-color: rgba(255, 255, 255, 0.12) !important;
        }

        .preview-status-ok {
          color: #34d399 !important;
        }

        .preview-status-error {
          color: #f87171 !important;
        }

        .preview-hint {
          color: #94a3b8 !important;
        }
      }
    `,
  ],
  template: `
    <h2 mat-dialog-title>{{ data.product ? 'Editar produto' : 'Novo produto' }}</h2>
    <mat-dialog-content>
      <form class="dialog-form" [formGroup]="form" (ngSubmit)="submit()">
        <mat-form-field appearance="outline">
          <mat-label>Nome</mat-label>
          <input matInput formControlName="name" maxlength="120" />
          <mat-icon matSuffix>restaurant_menu</mat-icon>
          @if (form.controls.name.hasError('required')) {
            <mat-error>Informe o nome do produto.</mat-error>
          }
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Categoria</mat-label>
          <mat-select formControlName="category">
            <mat-option value="Pizza">Pizza</mat-option>
            <mat-option value="Beverage">Bebida</mat-option>
            <mat-option value="Other">Outro</mat-option>
          </mat-select>
          <mat-icon matSuffix>category</mat-icon>
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>URL da imagem (opcional)</mat-label>
          <input
            matInput
            formControlName="imageUrl"
            placeholder="https://exemplo.com/fotos/calabresa.jpg"
          />
          <mat-icon matSuffix>add_photo_alternate</mat-icon>
          <mat-hint>Link direto para foto pública (PNG, JPG, WebP)</mat-hint>
        </mat-form-field>

        @if (imageUrlValue(); as url) {
          <div class="image-preview-card">
            <div class="image-preview-thumb">
              <mat-icon class="preview-fallback-icon">
                {{ form.controls.category.value === 'Beverage' ? 'local_drink' : 'local_pizza' }}
              </mat-icon>
              @if (!previewFailed()) {
                <img
                  [src]="url"
                  alt="Prévia da foto do produto"
                  class="preview-img"
                  (load)="onImgLoad()"
                  (error)="onImgError()"
                />
              }
            </div>
            <div class="image-preview-status">
              @if (previewFailed()) {
                <div class="preview-status-error">
                  <mat-icon>broken_image</mat-icon>
                  <span>Não foi possível carregar a imagem deste link.</span>
                </div>
                <span class="preview-hint">Verifique se o link está acessível publicamente (HTTPS).</span>
              } @else {
                <div class="preview-status-ok">
                  <mat-icon>check_circle</mat-icon>
                  <span>Prévia carregada com sucesso</span>
                </div>
                <span class="preview-hint">A imagem será exibida no cardápio público e na cozinha.</span>
              }
            </div>
          </div>
        }

        @if (form.controls.category.value === 'Beverage') {
          <mat-form-field appearance="outline">
            <mat-label>Preço de compra / custo (R$)</mat-label>
            <input matInput type="number" min="0" step="0.01" formControlName="purchasePrice" />
            <mat-icon matSuffix>payments</mat-icon>
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Markup multiplicador (ex.: 2 = dobro do custo)</mat-label>
            <input matInput type="number" min="0.01" step="0.1" formControlName="markupMultiplier" />
            <mat-icon matSuffix>trending_up</mat-icon>
          </mat-form-field>

          <p class="hint-text" style="margin: -4px 0 8px;">
            Preço de venda sugerido: <strong>{{ previewBeveragePrice() | brl }}</strong>
          </p>
        }

        <mat-slide-toggle color="primary" formControlName="isActive">
          Produto ativo
        </mat-slide-toggle>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button type="button" mat-dialog-close>Cancelar</button>
      <button mat-flat-button color="primary" type="button" (click)="submit()">Salvar</button>
    </mat-dialog-actions>
  `,
})
export class ProductFormDialog {
  readonly data = inject<ProductFormDialogData>(MAT_DIALOG_DATA);
  private readonly ref = inject<MatDialogRef<ProductFormDialog, ProductRequest>>(MatDialogRef);
  private readonly fb = inject(NonNullableFormBuilder);

  readonly previewFailed = signal(false);
  readonly imageUrlValue = signal<string>(this.data.product?.imageUrl?.trim() ?? '');

  readonly form = this.fb.group({
    name: [this.data.product?.name ?? '', [Validators.required, Validators.maxLength(120)]],
    category: [this.data.product?.category ?? 'Pizza', [Validators.required]],
    imageUrl: [this.data.product?.imageUrl ?? ''],
    purchasePrice: [this.data.product?.purchasePrice ?? 6.5, [Validators.min(0)]],
    markupMultiplier: [this.data.product?.markupMultiplier ?? 2.0, [Validators.min(0.01)]],
    isActive: [this.data.product?.isActive ?? true],
  });

  constructor() {
    this.form.controls.imageUrl.valueChanges.subscribe((val) => {
      const trimmed = (val ?? '').trim();
      this.previewFailed.set(false);
      this.imageUrlValue.set(trimmed);
    });
  }

  onImgLoad(): void {
    this.previewFailed.set(false);
  }

  onImgError(): void {
    this.previewFailed.set(true);
  }

  previewBeveragePrice(): number {
    const purchase = Number(this.form.controls.purchasePrice.value ?? 0);
    const markup = Number(this.form.controls.markupMultiplier.value ?? 2);
    if (purchase <= 0 || markup <= 0) return 0;
    return Number((purchase * markup).toFixed(2));
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const raw = this.form.getRawValue();
    const isBev = raw.category === 'Beverage';
    const trimmedImg = raw.imageUrl?.trim();
    this.ref.close({
      name: raw.name.trim(),
      category: raw.category,
      imageUrl: trimmedImg ? trimmedImg : null,
      isActive: raw.isActive,
      purchasePrice: isBev ? Number(raw.purchasePrice ?? 0) : null,
      markupMultiplier: isBev ? Number(raw.markupMultiplier ?? 2) : null,
    });
  }
}
