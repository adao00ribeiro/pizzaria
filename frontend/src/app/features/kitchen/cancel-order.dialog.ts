import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';

import { NonNullableFormControl } from '../../core/forms.compat';

export interface CancelOrderDialogData {
  code: string;
  alreadyReady?: boolean;
}

/**
 * Diálogo de confirmação para cancelamento de pedido na produção (docs/10, Sprint 3).
 * Exige o motivo do cancelamento e oferece atalhos touch-friendly para o tablet.
 */
@Component({
  selector: 'pz-cancel-order-dialog',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatChipsModule,
    MatIconModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: [
    `
      .cancel-dialog-text {
        margin: 0 0 12px;
        color: var(--pz-text-secondary, rgba(0, 0, 0, 0.7));
      }
      .reason-btn {
        font-size: 0.8125rem;
      }
      :host-context(.dark-theme) {
        .cancel-dialog-text {
          color: var(--pz-text-secondary, #cbd5e1);
        }
        .reason-btn {
          border-color: rgba(255, 255, 255, 0.15);
          color: var(--pz-text-main);
        }
      }
    `,
  ],
  template: `
    <h2 mat-dialog-title>Cancelar pedido #{{ data.code }}</h2>
    <mat-dialog-content>
      <p class="cancel-dialog-text">
        @if (data.alreadyReady) {
          Este pedido já foi marcado como <strong>Pronto</strong>. O cancelamento devolverá os
          ingredientes automaticamente ao estoque.
        } @else {
          Tem certeza que deseja cancelar este pedido? Informe o motivo abaixo:
        }
      </p>

      <div style="display: flex; flex-wrap: wrap; gap: 6px; margin-bottom: 12px;">
        @for (suggestion of suggestions; track suggestion) {
          <button
            mat-stroked-button
            type="button"
            class="reason-btn"
            (click)="selectReason(suggestion)"
          >
            {{ suggestion }}
          </button>
        }
      </div>

      <mat-form-field appearance="outline" class="full-width">
        <mat-label>Motivo do cancelamento</mat-label>
        <input
          matInput
          [formControl]="reasonControl"
          placeholder="Ex.: Cliente desistiu do pedido"
          maxlength="200"
        />
        @if (reasonControl.invalid && reasonControl.touched) {
          <mat-error>Informe o motivo do cancelamento.</mat-error>
        }
      </mat-form-field>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button type="button" mat-dialog-close>Voltar</button>
      <button mat-flat-button color="warn" type="button" (click)="confirm()">
        <mat-icon>cancel</mat-icon>
        Confirmar cancelamento
      </button>
    </mat-dialog-actions>
  `,
})
export class CancelOrderDialog {
  readonly data = inject<CancelOrderDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<CancelOrderDialog, string>);

  readonly suggestions = [
    'Cliente desistiu',
    'Pedido lançado duplicado',
    'Falta de ingrediente',
    'Erro no lançamento',
  ];

  readonly reasonControl = new NonNullableFormControl('', [
    Validators.required,
    Validators.minLength(3),
  ]);

  selectReason(reason: string): void {
    this.reasonControl.setValue(reason);
    this.reasonControl.markAsTouched();
  }

  confirm(): void {
    const trimmed = this.reasonControl.value.trim();
    this.reasonControl.setValue(trimmed);
    if (this.reasonControl.invalid) {
      this.reasonControl.markAsTouched();
      return;
    }
    this.dialogRef.close(trimmed);
  }
}
