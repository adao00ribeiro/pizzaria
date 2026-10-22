import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import {
  AbstractControl,
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  ValidationErrors,
  ValidatorFn,
  Validators,
} from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';

import { extractApiErrorMessage } from '../../../core/interceptors/error.interceptor';
import { AuthService } from '../../../core/services/auth.service';
import { ThemeService } from '../../../core/services/theme.service';

/** Política de senha do backend (NF-S02): 8+ caracteres, 1 maiúscula, 1 número. */
const passwordPolicy: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  const value = String(control.value ?? '');
  const errors: ValidationErrors = {};
  if (value.length < 8) {
    errors['minLength'] = true;
  }
  if (!/[A-Z]/.test(value)) {
    errors['uppercase'] = true;
  }
  if (!/[0-9]/.test(value)) {
    errors['number'] = true;
  }
  return Object.keys(errors).length > 0 ? errors : null;
};

const sameAsPassword: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  const parent = control.parent;
  if (!parent) {
    return null;
  }
  const password = parent.get('password')?.value;
  return control.value === password ? null : { mismatch: true };
};

/** Tela de cadastro — cria usuário + tenant (pizzaria) — docs/08 §3.1. */
@Component({
  selector: 'pz-register',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    MatSnackBarModule,
    MatTooltipModule,
    RouterLink,
  ],
  templateUrl: './register.component.html',
  styleUrl: '../login/auth-pages.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RegisterComponent {
  readonly auth = inject(AuthService);
  readonly theme = inject(ThemeService);
  private readonly router = inject(Router);
  private readonly snack = inject(MatSnackBar);

  readonly loading = signal(false);
  readonly hidePassword = signal(true);
  readonly hideConfirmPassword = signal(true);

  readonly form = new FormGroup({
    fullName: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.minLength(3)] }),
    pizzeriaName: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.minLength(2)] }),
    email: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.email] }),
    password: new FormControl('', { nonNullable: true, validators: [Validators.required, passwordPolicy] }),
    confirmPassword: new FormControl('', { nonNullable: true, validators: [Validators.required, sameAsPassword] }),
  });

  constructor() {
    if (this.auth.isAuthenticated()) {
      this.router.navigate(['/dashboard']);
    }
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const { fullName, pizzeriaName, email, password } = this.form.getRawValue();
    this.loading.set(true);
    this.auth
      .register({ fullName, pizzeriaName, email, password })
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: () => {
          this.snack.open('Conta criada! Bem-vindo(a).', 'Fechar', {
            duration: 4000,
            panelClass: 'success-snack',
          });
          this.router.navigate(['/dashboard']);
        },
        error: (err: HttpErrorResponse) => {
          this.snack.open(extractApiErrorMessage(err), 'Fechar', {
            duration: 6000,
            panelClass: 'error-snack',
          });
        },
      });
  }
}
