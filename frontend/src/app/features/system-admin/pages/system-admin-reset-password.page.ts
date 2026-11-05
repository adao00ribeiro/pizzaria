import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';

import { SystemAdminService } from '../../../core/services/system-admin.service';
import { ThemeService } from '../../../core/services/theme.service';

@Component({
  selector: 'pz-system-admin-reset-password',
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
    RouterLink,
  ],
  template: `
    <div class="reset-wrapper">
      <!-- Botão Flutuante de Alternar Tema -->
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

      <mat-card class="reset-card">
        @if (loading()) {
          <mat-progress-bar mode="indeterminate"></mat-progress-bar>
        }

        <div class="card-header">
          <div class="badge-icon">
            <mat-icon>vpn_key</mat-icon>
          </div>
          <h1>Redefinir Senha</h1>
          <p class="subtitle">Administrador do Sistema</p>
        </div>

        @if (!success()) {
          <form [formGroup]="resetForm" (ngSubmit)="submit()">
            <mat-form-field appearance="outline" class="full-width">
              <mat-label>Token de Recuperação</mat-label>
              <input
                matInput
                formControlName="token"
                placeholder="Cole o token recebido"
              />
              <mat-icon matPrefix>key</mat-icon>
              @if (resetForm.controls.token.invalid && resetForm.controls.token.touched) {
                <mat-error>Token obrigatório</mat-error>
              }
            </mat-form-field>

            <mat-form-field appearance="outline" class="full-width">
              <mat-label>Nova Senha</mat-label>
              <input
                matInput
                [type]="hidePassword() ? 'password' : 'text'"
                formControlName="newPassword"
              />
              <mat-icon matPrefix>lock</mat-icon>
              <button
                mat-icon-button
                matSuffix
                type="button"
                (click)="hidePassword.set(!hidePassword())"
              >
                <mat-icon>{{ hidePassword() ? 'visibility_off' : 'visibility' }}</mat-icon>
              </button>
              <mat-hint>Mínimo 8 caracteres, com maiúscula, número e símbolo</mat-hint>
              @if (resetForm.controls.newPassword.invalid && resetForm.controls.newPassword.touched) {
                <mat-error>Senha não atende aos requisitos mínimos de segurança</mat-error>
              }
            </mat-form-field>

            <mat-form-field appearance="outline" class="full-width">
              <mat-label>Confirmar Nova Senha</mat-label>
              <input
                matInput
                [type]="hidePassword() ? 'password' : 'text'"
                formControlName="confirmPassword"
              />
              <mat-icon matPrefix>lock_outline</mat-icon>
              @if (resetForm.hasError('mismatch') && resetForm.controls.confirmPassword.touched) {
                <mat-error>As senhas não coincidem</mat-error>
              }
            </mat-form-field>

            <div class="actions">
              <button
                mat-flat-button
                color="primary"
                type="submit"
                class="submit-btn"
                [disabled]="loading() || resetForm.invalid"
              >
                Salvar Nova Senha
              </button>
            </div>

            <div class="back-link">
              <a mat-button routerLink="/system-admin/login">
                Voltar para o Login
              </a>
            </div>
          </form>
        } @else {
          <div class="success-box">
            <mat-icon class="success-icon">check_circle</mat-icon>
            <h2>Senha Atualizada com Sucesso!</h2>
            <p>Sua senha administrativa foi redefinida com segurança.</p>
            <a mat-flat-button color="primary" routerLink="/system-admin/login" class="submit-btn">
              Ir para o Login
            </a>
          </div>
        }
      </mat-card>
    </div>
  `,
  styles: [`
    .reset-wrapper {
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

    .reset-card {
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
      background: var(--pz-info-light, #e0f2fe);
      color: var(--pz-info, #0284c7);
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

    .full-width {
      width: 100%;
      margin-bottom: 1.25rem;
    }

    .actions {
      margin-top: 1rem;
    }

    .submit-btn {
      width: 100%;
      height: 48px;
      font-weight: 700;
      font-size: 1rem;
      border-radius: 8px;
    }

    .back-link {
      margin-top: 1rem;
      text-align: center;
    }

    .success-box {
      text-align: center;
      padding: 1rem 0;

      .success-icon {
        font-size: 48px;
        width: 48px;
        height: 48px;
        color: var(--pz-success, #059669);
        margin-bottom: 1rem;
      }

      h2 {
        font-size: 1.3rem;
        font-weight: 700;
        margin-bottom: 0.5rem;
        color: var(--pz-text-main, #0f172a);
      }

      p {
        color: var(--pz-text-secondary, #475569);
        font-size: 0.9rem;
        margin-bottom: 1.5rem;
      }
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SystemAdminResetPasswordPage implements OnInit {
  private readonly adminService = inject(SystemAdminService);
  private readonly route = inject(ActivatedRoute);
  private readonly snack = inject(MatSnackBar);
  readonly theme = inject(ThemeService);

  readonly loading = signal(false);
  readonly hidePassword = signal(true);
  readonly success = signal(false);

  readonly resetForm = new FormGroup(
    {
      token: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
      newPassword: new FormControl('', {
        nonNullable: true,
        validators: [
          Validators.required,
          Validators.minLength(8),
          Validators.pattern(/^(?=.*[A-Z])(?=.*[0-9])(?=.*[^a-zA-Z0-9]).*$/),
        ],
      }),
      confirmPassword: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    },
    {
      validators: (group) => {
        const pass = group.get('newPassword')?.value;
        const confirm = group.get('confirmPassword')?.value;
        return pass === confirm ? null : { mismatch: true };
      },
    }
  );

  ngOnInit(): void {
    const token = this.route.snapshot.queryParamMap.get('token');
    if (token) {
      this.resetForm.controls.token.setValue(token);
    }
  }

  submit(): void {
    if (this.resetForm.invalid) {
      this.resetForm.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    const { token, newPassword } = this.resetForm.getRawValue();

    this.adminService
      .resetPassword({ token, newPassword })
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: () => {
          this.success.set(true);
        },
        error: (err) => {
          const message = err.error?.message || 'Token inválido ou expirado.';
          this.snack.open(message, 'Fechar', {
            duration: 5000,
            panelClass: 'error-snack',
          });
        },
      });
  }
}
