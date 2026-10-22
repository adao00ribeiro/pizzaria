import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';

import { NonNullableFormControl, NonNullableFormGroup } from '../../core/forms.compat';
import { ROLE_DESCRIPTIONS, ROLE_LABELS, USER_ROLES, User, UserRole } from '../../core/models/user.models';

export interface UserDialogData {
  user?: User;
}

export type UserDialogResult =
  | { mode: 'create'; email: string; password: string; fullName: string; role: UserRole | string }
  | { mode: 'edit'; fullName: string; role: UserRole | string };

/**
 * Diálogo de criação/edição de usuário da equipe (docs/10, Sprint 1).
 * Criação pede e-mail + senha; edição permite trocar nome e papel.
 */
@Component({
  selector: 'pz-user-form-dialog',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatIconModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ editing ? 'Editar usuário' : 'Novo usuário' }}</h2>

    <mat-dialog-content>
      <form [formGroup]="form" (ngSubmit)="save()" novalidate>
        @if (editing) {
          <p class="hint-text" style="margin-top: 0;">{{ data.user?.email }}</p>
        } @else {
          <mat-form-field appearance="outline" class="full-width">
            <mat-label>E-mail</mat-label>
            <input matInput type="email" formControlName="email" placeholder="pessoa@pizzaria.com" />
            @if (form.controls.email.hasError('required')) {
              <mat-error>E-mail obrigatório</mat-error>
            } @else if (form.controls.email.hasError('email')) {
              <mat-error>Informe um e-mail válido</mat-error>
            }
          </mat-form-field>

          <mat-form-field appearance="outline" class="full-width">
            <mat-label>Senha provisória</mat-label>
            <input matInput type="password" formControlName="password" />
            <mat-hint>Mínimo 6 caracteres, com letra e número.</mat-hint>
            @if (form.controls.password.hasError('required')) {
              <mat-error>Senha obrigatória</mat-error>
            } @else if (form.controls.password.hasError('minlength')) {
              <mat-error>Mínimo de 6 caracteres</mat-error>
            }
          </mat-form-field>
        }

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Nome completo</mat-label>
          <input matInput formControlName="fullName" placeholder="Ex.: Ana Souza" />
          @if (form.controls.fullName.hasError('required')) {
            <mat-error>Nome obrigatório</mat-error>
          }
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Papel</mat-label>
          <mat-select formControlName="role">
            @for (role of roles; track role) {
              <mat-option [value]="role">{{ labels[role] }}</mat-option>
            }
          </mat-select>
          <mat-hint>
            {{ descriptions[form.controls.role.value] || 'Selecione o papel do colaborador.' }}
          </mat-hint>
        </mat-form-field>
      </form>
    </mat-dialog-content>

    <mat-dialog-actions align="end">
      <button mat-button type="button" mat-dialog-close>Cancelar</button>
      <button mat-flat-button color="primary" type="button" (click)="save()">
        {{ editing ? 'Salvar' : 'Cadastrar' }}
      </button>
    </mat-dialog-actions>
  `,
})
export class UserFormDialog {
  readonly data = inject<UserDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject<MatDialogRef<UserFormDialog, UserDialogResult>>(MatDialogRef);

  readonly editing = !!this.data.user;
  readonly roles: UserRole[] = [...USER_ROLES];
  readonly labels = ROLE_LABELS;
  readonly descriptions = ROLE_DESCRIPTIONS;

  readonly form = new NonNullableFormGroup({
    email: new NonNullableFormControl(this.data.user?.email ?? '', [
      Validators.required,
      Validators.email,
    ]),
    password: new NonNullableFormControl('', this.editing ? [] : [Validators.required, Validators.minLength(6)]),
    fullName: new NonNullableFormControl(this.data.user?.fullName ?? '', [Validators.required]),
    role: new NonNullableFormControl<UserRole | string>(this.data.user?.role ?? 'atendente', [
      Validators.required,
    ]),
  });

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const value = this.form.getRawValue();
    if (this.editing) {
      this.dialogRef.close({
        mode: 'edit',
        fullName: value.fullName.trim(),
        role: value.role,
      });
    } else {
      this.dialogRef.close({
        mode: 'create',
        email: value.email.trim(),
        password: value.password,
        fullName: value.fullName.trim(),
        role: value.role,
      });
    }
  }
}
