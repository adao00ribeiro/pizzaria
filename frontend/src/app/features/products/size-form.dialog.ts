import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';

import { Size, SizeRequest } from '../../core/models/product.models';

/** Dados do diálogo de tamanho. */
export interface SizeFormDialogData {
  size?: Size;
}

/**
 * Diálogo de criação/edição de tamanho de pizza (docs/07 §5).
 * Resolve com o body pronto para `POST/PUT /sizes`.
 */
@Component({
  selector: 'pz-size-form-dialog',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatIconModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ data.size ? 'Editar tamanho' : 'Novo tamanho' }}</h2>
    <mat-dialog-content>
      <form class="dialog-form" [formGroup]="form" (ngSubmit)="submit()">
        <mat-form-field appearance="outline">
          <mat-label>Nome</mat-label>
          <input matInput formControlName="name" maxlength="60" placeholder="Ex.: Grande" />
          <mat-icon matSuffix>straighten</mat-icon>
          @if (form.controls.name.hasError('required')) {
            <mat-error>Informe o nome do tamanho.</mat-error>
          }
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Diâmetro (cm)</mat-label>
          <input matInput type="number" min="1" step="1" formControlName="diameterCm" />
          <mat-icon matSuffix>radio_button_unchecked</mat-icon>
          @if (form.controls.diameterCm.invalid) {
            <mat-error>Informe um diâmetro maior que zero.</mat-error>
          }
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button type="button" mat-dialog-close>Cancelar</button>
      <button mat-flat-button color="primary" type="button" (click)="submit()">Salvar</button>
    </mat-dialog-actions>
  `,
})
export class SizeFormDialog {
  readonly data = inject<SizeFormDialogData>(MAT_DIALOG_DATA);
  private readonly ref = inject<MatDialogRef<SizeFormDialog, SizeRequest>>(MatDialogRef);
  private readonly fb = inject(NonNullableFormBuilder);

  readonly form = this.fb.group({
    name: [this.data.size?.name ?? '', [Validators.required, Validators.maxLength(60)]],
    diameterCm: [this.data.size?.diameterCm ?? 0, [Validators.required, Validators.min(1)]],
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.ref.close(this.form.getRawValue());
  }
}
