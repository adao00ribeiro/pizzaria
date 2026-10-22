import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import {
  FormControl,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { finalize } from 'rxjs';

import { Fees } from '../../core/models/cost.models';
import { MoneyInputDirective } from '../../shared/components/money-input';
import { TenantService } from '../../core/services/tenant.service';
import { FeesService } from './fees.service';
import { NonNullableFormControl, NonNullableFormGroup } from '../../core/forms.compat';

/**
 * Taxas e margem padrão do tenant (docs/08 §3.2 — Cadastros › Taxas).
 * Estes valores entram na formação do preço sugerido de todos os itens.
 */
@Component({
  selector: 'pz-fees-page',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
    MoneyInputDirective,
  ],
  templateUrl: './fees.page.html',
  styleUrl: './fees.page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FeesPage {
  private readonly service = inject(FeesService);
  private readonly tenants = inject(TenantService);
  private readonly snack = inject(MatSnackBar);

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly savingSlug = signal(false);

  /** Nome e slug público da pizzaria (docs/10, Sprint 1). */
  readonly tenantForm = new NonNullableFormGroup({
    name: new NonNullableFormControl('', [Validators.required, Validators.maxLength(120)]),
    slug: new NonNullableFormControl('', [
      Validators.required,
      Validators.minLength(3),
      Validators.maxLength(40),
      Validators.pattern(/^[a-z0-9](?:[a-z0-9-]*[a-z0-9])?$/),
    ]),
  });

  readonly form = new NonNullableFormGroup({
    cardFeePercentage: new FormControl<number | null>(2.99, [Validators.required, Validators.min(0), Validators.max(100)]),
    deliveryFeePercentage: new FormControl<number | null>(0, [Validators.required, Validators.min(0), Validators.max(100)]),
    deliveryFixedValue: new FormControl<number | null>(0, [Validators.required, Validators.min(0)]),
    defaultMarginPercentage: new FormControl<number | null>(60, [Validators.required, Validators.min(0), Validators.max(100)]),
    lossPercentageGlobal: new FormControl<number | null>(0, [Validators.required, Validators.min(0), Validators.max(100)]),
  });

  constructor() {
    this.load();
    this.loadTenant();
  }

  loadTenant(): void {
    this.tenants.ensureLoaded().subscribe({
      next: (tenant) => {
        this.tenantForm.patchValue({
          name: tenant.name ?? '',
          slug: tenant.slug ?? '',
        });
      },
      error: () => undefined,
    });
  }

  saveTenant(): void {
    if (this.tenantForm.invalid) {
      this.tenantForm.markAllAsTouched();
      return;
    }
    const value = this.tenantForm.getRawValue();
    this.savingSlug.set(true);
    this.tenants
      .update({ name: value.name.trim(), slug: value.slug.trim().toLowerCase() })
      .pipe(finalize(() => this.savingSlug.set(false)))
      .subscribe({
        next: () => {
          this.snack.open('Dados da pizzaria salvos.', 'Fechar', {
            duration: 3000,
            panelClass: 'success-snack',
          });
        },
        error: () => undefined,
      });
  }

  copyLink(): void {
    const slug = this.tenantForm.controls.slug.value;
    const url = `${window.location.origin}/menu/${slug}`;
    void navigator.clipboard?.writeText(url);
    this.snack.open('Link do cardápio copiado.', 'Fechar', { duration: 3000 });
  }

  load(): void {
    this.loading.set(true);
    this.service
      .get()
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (fees) => this.patch(fees),
        error: () => undefined,
      });
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const value = this.form.getRawValue();
    const body: Fees = {
      cardFeePercentage: Number(value.cardFeePercentage ?? 0),
      deliveryFeePercentage: Number(value.deliveryFeePercentage ?? 0),
      deliveryFixedValue: Number(value.deliveryFixedValue ?? 0),
      defaultMarginPercentage: Number(value.defaultMarginPercentage ?? 0),
      lossPercentageGlobal: Number(value.lossPercentageGlobal ?? 0),
    };
    this.saving.set(true);
    this.service
      .update(body)
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: (fees) => {
          this.patch(fees ?? body);
          this.snack.open('Taxas salvas. Os preços serão recalculados.', 'Fechar', {
            duration: 4000,
            panelClass: 'success-snack',
          });
        },
        error: () => undefined,
      });
  }

  private patch(fees: Fees): void {
    this.form.patchValue({
      cardFeePercentage: fees.cardFeePercentage,
      deliveryFeePercentage: fees.deliveryFeePercentage,
      deliveryFixedValue: fees.deliveryFixedValue,
      defaultMarginPercentage: fees.defaultMarginPercentage,
      lossPercentageGlobal: fees.lossPercentageGlobal,
    });
  }
}
