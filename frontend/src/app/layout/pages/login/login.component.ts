import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { ReactiveFormsModule, Validators } from '@angular/forms';
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
import { NonNullableFormControl, NonNullableFormGroup } from '../../../core/forms.compat';

/** Tela de login (docs/08 §3.1) com suporte a tema claro/escuro e identidade Posachio. */
@Component({
  selector: 'pz-login',
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
  templateUrl: './login.component.html',
  styleUrl: './auth-pages.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LoginComponent {
  readonly auth = inject(AuthService);
  readonly theme = inject(ThemeService);
  private readonly router = inject(Router);
  private readonly snack = inject(MatSnackBar);

  readonly loading = signal(false);
  readonly hidePassword = signal(true);

  readonly form = new NonNullableFormGroup({
    email: new NonNullableFormControl('', [Validators.required, Validators.email]),
    password: new NonNullableFormControl('', [Validators.required]),
  });

  constructor() {
    if (this.auth.isAuthenticated()) {
      this.router.navigate([this.auth.homeRouteForRole()]);
    }
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.loading.set(true);
    this.auth
      .login(this.form.getRawValue())
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: () => this.router.navigate([this.auth.homeRouteForRole()]),
        error: (err: HttpErrorResponse) => {
          const message =
            err.status === 401
              ? 'E-mail ou senha inválidos. Verifique os dados informados.'
              : extractApiErrorMessage(err);
          this.snack.open(message, 'Fechar', { duration: 6000, panelClass: 'error-snack' });
        },
      });
  }
}
