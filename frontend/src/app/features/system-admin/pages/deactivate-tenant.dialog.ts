import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';

export interface DeactivateTenantDialogData {
  tenantName: string;
}

@Component({
  selector: 'pz-deactivate-tenant-dialog',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
  ],
  template: `
    <h2 mat-dialog-title class="dialog-title">
      <mat-icon color="warn">warning</mat-icon>
      Desativar Pizzaria
    </h2>

    <mat-dialog-content>
      <p class="description">
        Você está prestes a suspender o acesso da pizzaria
        <strong>{{ data.tenantName }}</strong>.
      </p>

      <div class="warning-callout">
        <mat-icon>info</mat-icon>
        <span>
          O corte é imediato: novos logins, renovações de sessão, requisições ativas e o
          cardápio público serão suspensos imediatamente.
        </span>
      </div>

      <form [formGroup]="form">
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Motivo Administrativo da Suspensão</mat-label>
          <textarea
            matInput
            rows="3"
            formControlName="reason"
            placeholder="Ex.: Inadimplência da mensalidade referente ao mês vigente."
          ></textarea>
          <mat-hint align="end">{{ form.controls.reason.value.length }}/300</mat-hint>
          @if (form.controls.reason.invalid && form.controls.reason.touched) {
            <mat-error>
              Informe o motivo com pelo menos 5 e no máximo 300 caracteres.
            </mat-error>
          }
        </mat-form-field>
      </form>
    </mat-dialog-content>

    <mat-dialog-actions align="end">
      <button mat-button type="button" mat-dialog-close>Cancelar</button>
      <button
        mat-flat-button
        color="warn"
        type="button"
        [disabled]="form.invalid"
        (click)="confirm()"
      >
        Confirmar Suspensão
      </button>
    </mat-dialog-actions>
  `,
  styles: [`
    .dialog-title {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      margin: 0;
      color: var(--pz-text-main, #0f172a);
      font-weight: 700;
    }

    .description {
      margin-top: 0.5rem;
      margin-bottom: 1rem;
      font-size: 0.95rem;
      color: var(--pz-text-secondary, #475569);
      line-height: 1.4;

      strong {
        color: var(--pz-text-main, #0f172a);
      }
    }

    .warning-callout {
      display: flex;
      gap: 0.75rem;
      background: var(--pz-danger-light, #fef2f2);
      border-left: 4px solid var(--pz-danger, #dc2626);
      padding: 0.75rem 1rem;
      border-radius: 6px;
      margin-bottom: 1.25rem;
      font-size: 0.85rem;
      color: var(--pz-text-main, #0f172a);
      line-height: 1.4;

      mat-icon {
        color: var(--pz-danger, #dc2626);
        font-size: 20px;
        width: 20px;
        height: 20px;
        flex-shrink: 0;
      }
    }

    .full-width {
      width: 100%;
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DeactivateTenantDialog {
  readonly data = inject<DeactivateTenantDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<DeactivateTenantDialog>);

  readonly form = new FormGroup({
    reason: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.minLength(5), Validators.maxLength(300)],
    }),
  });

  confirm(): void {
    if (this.form.invalid) return;
    this.dialogRef.close(this.form.controls.reason.value.trim());
  }
}
