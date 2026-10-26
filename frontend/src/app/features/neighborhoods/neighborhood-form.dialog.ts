import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';

import {
  NEIGHBORHOOD_REGION_LABELS,
  NEIGHBORHOOD_REGIONS,
  NeighborhoodDto,
  NeighborhoodInputDto,
  NeighborhoodRegion,
} from '../../core/models/neighborhood.models';
import { NeighborhoodsService } from './neighborhoods.service';

export interface NeighborhoodDialogData {
  neighborhood?: NeighborhoodDto;
}

@Component({
  selector: 'pz-neighborhood-form-dialog',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatCheckboxModule,
    MatIconModule,
    MatProgressSpinnerModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ data.neighborhood ? 'Editar bairro' : 'Novo bairro' }}</h2>

    <mat-dialog-content>
      @if (errorMessage()) {
        <div class="dialog-error-banner">
          <mat-icon>error_outline</mat-icon>
          <span>{{ errorMessage() }}</span>
        </div>
      }

      <form [formGroup]="form" (ngSubmit)="submit()" class="dialog-form" novalidate>
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Nome do Bairro</mat-label>
          <input
            matInput
            formControlName="name"
            placeholder="Ex.: Centro, Jardim das Flores, Bela Vista"
            maxlength="100"
          />
          @if (form.controls.name.hasError('required')) {
            <mat-error>O nome do bairro é obrigatório.</mat-error>
          } @else if (form.controls.name.hasError('minlength')) {
            <mat-error>Mínimo de 2 caracteres.</mat-error>
          }
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Região da Cidade</mat-label>
          <mat-select formControlName="region">
            @for (reg of regions; track reg) {
              <mat-option [value]="reg">
                {{ regionLabels[reg] }}
              </mat-option>
            }
          </mat-select>
          @if (form.controls.region.hasError('required')) {
            <mat-error>A região é obrigatória.</mat-error>
          }
        </mat-form-field>

        <div class="checkbox-row">
          <mat-checkbox formControlName="isActive" color="primary">
            Bairro ativo para entregas e pedidos
          </mat-checkbox>
        </div>
      </form>
    </mat-dialog-content>

    <mat-dialog-actions align="end">
      <button mat-button type="button" mat-dialog-close [disabled]="submitting()">
        Cancelar
      </button>
      <button
        mat-flat-button
        color="primary"
        type="button"
        (click)="submit()"
        [disabled]="form.invalid || submitting()"
      >
        @if (submitting()) {
          <mat-spinner diameter="18" />
        } @else {
          {{ data.neighborhood ? 'Salvar alterações' : 'Cadastrar bairro' }}
        }
      </button>
    </mat-dialog-actions>
  `,
  styles: [
    `
      .full-width {
        width: 100%;
        margin-bottom: 12px;
      }

      .dialog-form {
        display: flex;
        flex-direction: column;
        padding-top: 8px;
        min-width: 320px;
      }

      .checkbox-row {
        margin: 4px 0 16px;
      }

      .dialog-error-banner {
        display: flex;
        align-items: center;
        gap: 8px;
        background: #fee2e2;
        color: #991b1b;
        border: 1px solid #fecaca;
        border-radius: 8px;
        padding: 8px 12px;
        margin-bottom: 12px;
        font-size: 0.85rem;

        mat-icon {
          font-size: 18px;
          width: 18px;
          height: 18px;
        }
      }
    `,
  ],
})
export class NeighborhoodFormDialog {
  readonly data = inject<NeighborhoodDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<NeighborhoodFormDialog>);
  private readonly service = inject(NeighborhoodsService);

  readonly regions = NEIGHBORHOOD_REGIONS;
  readonly regionLabels = NEIGHBORHOOD_REGION_LABELS;

  readonly submitting = signal(false);
  readonly errorMessage = signal<string | null>(null);

  readonly form = new FormGroup({
    name: new FormControl(this.data.neighborhood?.name ?? '', {
      nonNullable: true,
      validators: [Validators.required, Validators.minLength(2), Validators.maxLength(100)],
    }),
    region: new FormControl<NeighborhoodRegion>(this.data.neighborhood?.region ?? 'Center', {
      nonNullable: true,
      validators: [Validators.required],
    }),
    isActive: new FormControl(this.data.neighborhood?.isActive ?? true, {
      nonNullable: true,
    }),
  });

  submit(): void {
    if (this.form.invalid || this.submitting()) return;

    this.submitting.set(true);
    this.errorMessage.set(null);

    const val = this.form.getRawValue();
    const payload: NeighborhoodInputDto = {
      name: val.name.trim(),
      region: val.region,
      isActive: val.isActive,
    };

    const action$ = this.data.neighborhood
      ? this.service.update(this.data.neighborhood.id, payload)
      : this.service.create(payload);

    action$.subscribe({
      next: (saved) => {
        this.submitting.set(false);
        this.dialogRef.close(saved);
      },
      error: (err) => {
        this.submitting.set(false);
        const detail = err?.error?.message || err?.error?.errors?.[0]?.detail;
        if (detail && detail.toLowerCase().includes('já cadastrado')) {
          this.errorMessage.set(`O bairro "${payload.name}" já está cadastrado nesta pizzaria.`);
        } else {
          this.errorMessage.set(detail || 'Erro ao salvar o bairro. Tente novamente.');
        }
      },
    });
  }
}
