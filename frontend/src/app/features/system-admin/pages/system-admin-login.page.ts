import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Router } from '@angular/router';
import { finalize } from 'rxjs';

import { SystemAdminService } from '../../../core/services/system-admin.service';
import { ThemeService } from '../../../core/services/theme.service';

@Component({
  selector: 'pz-system-admin-login',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    MatSnackBarModule,
    MatTooltipModule,
  ],
  template: `
    <div class="login-wrapper">
      <!-- Botão Flutuante de Tema (Claro / Escuro) -->
      <button
        mat-icon-button
        type="button"
        class="theme-toggle-floating"
        (click)="theme.toggleTheme()"
        [matTooltip]="theme.isDark() ? 'Alternar para Tema Claro' : 'Alternar para Tema Escuro'"
        aria-label="Alternar Tema Claro e Escuro"
      >
        <mat-icon>{{ theme.isDark() ? 'light_mode' : 'dark_mode' }}</mat-icon>
      </button>

      <mat-card class="login-card">
        @if (loading()) {
          <mat-progress-bar mode="indeterminate"></mat-progress-bar>
        }

        <div class="card-header">
          <div class="badge-icon">
            <mat-icon>security</mat-icon>
          </div>
          <h1>Governança Global</h1>
          <p class="subtitle">Acesso Exclusivo do Administrador do Sistema</p>
        </div>

        @if (!forgotPasswordMode()) {
          <form [formGroup]="loginForm" (ngSubmit)="submitLogin()">
            <mat-form-field appearance="outline" class="full-width">
              <mat-label>E-mail do Administrador</mat-label>
              <input
                matInput
                type="email"
                formControlName="email"
                placeholder="admin@plataforma.com"
                autocomplete="username"
              />
              <mat-icon matPrefix>email</mat-icon>
              @if (loginForm.controls.email.invalid && loginForm.controls.email.touched) {
                <mat-error>Informe um e-mail válido</mat-error>
              }
            </mat-form-field>

            <mat-form-field appearance="outline" class="full-width">
              <mat-label>Senha</mat-label>
              <input
                matInput
                [type]="hidePassword() ? 'password' : 'text'"
                formControlName="password"
                autocomplete="current-password"
              />
              <mat-icon matPrefix>lock</mat-icon>
              <button
                mat-icon-button
                matSuffix
                type="button"
                (click)="hidePassword.set(!hidePassword())"
                [attr.aria-label]="hidePassword() ? 'Exibir senha' : 'Ocultar senha'"
              >
                <mat-icon>{{ hidePassword() ? 'visibility_off' : 'visibility' }}</mat-icon>
              </button>
              @if (loginForm.controls.password.invalid && loginForm.controls.password.touched) {
                <mat-error>Senha obrigatória</mat-error>
              }
            </mat-form-field>

            <div class="actions">
              <button
                mat-flat-button
                color="primary"
                type="submit"
                class="submit-btn"
                [disabled]="loading()"
              >
                Entrar no Painel
              </button>
            </div>

            <div class="forgot-wrapper">
              <button
                mat-button
                type="button"
                color="accent"
                (click)="forgotPasswordMode.set(true)"
              >
                Esqueci minha senha
              </button>
            </div>
          </form>
        } @else {
          <form [formGroup]="forgotForm" (ngSubmit)="submitForgotPassword()">
            <p class="info-text">
              Informe seu e-mail cadastrado para solicitar a recuperação da conta do administrador.
            </p>

            <mat-form-field appearance="outline" class="full-width">
              <mat-label>E-mail Cadastrado</mat-label>
              <input
                matInput
                type="email"
                formControlName="email"
                placeholder="admin@plataforma.com"
              />
              <mat-icon matPrefix>email</mat-icon>
            </mat-form-field>

            <div class="actions">
              <button
                mat-flat-button
                color="primary"
                type="submit"
                class="submit-btn"
                [disabled]="loading() || forgotForm.invalid"
              >
                Solicitar Recuperação
              </button>
            </div>

            <div class="forgot-wrapper">
              <button
                mat-button
                type="button"
                (click)="forgotPasswordMode.set(false)"
              >
                Voltar para o Login
              </button>
            </div>
          </form>
        }
      </mat-card>
    </div>
  `,
  styles: [`
    .login-wrapper {
      position: relative;
      min-height: 100vh;
      display: flex;
      align-items: center;
      justify-content: center;
      background-color: var(--pz-bg-app, #f8fafc);
      padding: 1.5rem;
      transition: background-color 0.2s ease;
    }

    .theme-toggle-floating {
      position: absolute;
      top: 20px;
      right: 20px;
      color: var(--pz-text-secondary, #475569);
      background: var(--pz-bg-card, #ffffff);
      border: 1px solid var(--pz-border, #e2e8f0);
      box-shadow: var(--pz-shadow-card, 0 1px 3px rgba(0, 0, 0, 0.04));

      &:hover {
        background: var(--pz-bg-card-hover, #f1f5f9);
        color: var(--pz-text-main, #0f172a);
      }
    }

    .login-card {
      width: 100%;
      max-width: 440px;
      padding: 2.5rem 2rem;
      border-radius: 16px;
      background-color: var(--pz-bg-card, #ffffff);
      border: 1px solid var(--pz-border, #e2e8f0);
      box-shadow: var(--pz-shadow-hover, 0 10px 25px -5px rgba(0, 0, 0, 0.08));
      color: var(--pz-text-main, #0f172a);
      transition: background-color 0.2s ease, border-color 0.2s ease;
    }

    .card-header {
      text-align: center;
      margin-bottom: 2rem;
    }

    .badge-icon {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 60px;
      height: 60px;
      border-radius: 50%;
      background: var(--pz-primary-light, #fef2f2);
      color: var(--pz-primary, #c62828);
      border: 1px solid var(--pz-primary-border, #fecaca);
      margin-bottom: 1.25rem;

      mat-icon {
        font-size: 32px;
        width: 32px;
        height: 32px;
      }
    }

    h1 {
      font-size: 1.55rem;
      font-weight: 800;
      margin: 0;
      color: var(--pz-text-main, #0f172a);
      letter-spacing: -0.02em;
    }

    .subtitle {
      font-size: 0.88rem;
      color: var(--pz-text-muted, #64748b);
      margin-top: 0.35rem;
    }

    .info-text {
      font-size: 0.88rem;
      color: var(--pz-text-secondary, #475569);
      margin-bottom: 1.5rem;
      text-align: center;
      line-height: 1.5;
    }

    .full-width {
      width: 100%;
      margin-bottom: 1rem;
    }

    .actions {
      margin-top: 0.5rem;
      margin-bottom: 1rem;
    }

    .submit-btn {
      width: 100%;
      height: 48px;
      font-weight: 700;
      font-size: 1rem;
      border-radius: 8px;
    }

    .forgot-wrapper {
      text-align: center;
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SystemAdminLoginPage {
  private readonly adminService = inject(SystemAdminService);
  private readonly router = inject(Router);
  private readonly snack = inject(MatSnackBar);
  readonly theme = inject(ThemeService);

  readonly loading = signal(false);
  readonly hidePassword = signal(true);
  readonly forgotPasswordMode = signal(false);

  readonly loginForm = new FormGroup({
    email: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.email] }),
    password: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
  });

  readonly forgotForm = new FormGroup({
    email: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.email] }),
  });

  constructor() {
    if (this.adminService.isAuthenticated()) {
      this.router.navigate(['/system-admin/tenants']);
    }
  }

  submitLogin(): void {
    if (this.loginForm.invalid) {
      this.loginForm.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    const { email, password } = this.loginForm.getRawValue();

    this.adminService
      .login({ email, password })
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: () => {
          this.snack.open('Autenticado com sucesso!', 'Fechar', {
            duration: 3000,
            panelClass: 'success-snack',
          });
          this.router.navigate(['/system-admin/tenants']);
        },
        error: (err) => {
          const message = err.error?.message || 'E-mail ou senha inválidos.';
          this.snack.open(message, 'Fechar', {
            duration: 5000,
            panelClass: 'error-snack',
          });
        },
      });
  }

  submitForgotPassword(): void {
    if (this.forgotForm.invalid) {
      this.forgotForm.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    const { email } = this.forgotForm.getRawValue();

    this.adminService
      .forgotPassword({ email })
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (res) => {
          this.snack.open(res.message, 'OK', {
            duration: 6000,
          });
          this.forgotPasswordMode.set(false);
        },
        error: () => {
          this.snack.open(
            'Se o e-mail informado for válido, as instruções foram processadas.',
            'OK',
            { duration: 6000 }
          );
          this.forgotPasswordMode.set(false);
        },
      });
  }
}
