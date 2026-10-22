import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { finalize } from 'rxjs';

import { ConfirmDialog } from '../../shared/components/confirm-dialog';
import { ROLE_LABELS, User } from '../../core/models/user.models';
import { TeamService } from './team.service';
import { UserDialogResult, UserFormDialog } from './user-form.dialog';

/**
 * Equipe: usuários e papéis do tenant (docs/10, Sprint 1). Só o owner acessa.
 */
@Component({
  selector: 'pz-team-page',
  standalone: true,
  imports: [
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatChipsModule,
    MatDialogModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
    MatTooltipModule,
    DatePipe,
  ],
  templateUrl: './team.page.html',
  styleUrl: './team.page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TeamPage {
  private readonly service = inject(TeamService);
  private readonly dialog = inject(MatDialog);
  private readonly snack = inject(MatSnackBar);

  readonly columns = ['fullName', 'email', 'role', 'createdAt', 'actions'];
  readonly roleLabels = ROLE_LABELS;

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly users = signal<User[]>([]);

  readonly ownerCount = computed(() => this.users().filter((u) => u.isOwner).length);

  constructor() {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.service
      .list()
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (users) => this.users.set(users ?? []),
        error: () => undefined,
      });
  }

  roleLabel(role: string): string {
    return this.roleLabels[role] ?? role;
  }

  openCreate(): void {
    this.openDialog().subscribe((result) => {
      if (result?.mode !== 'create') {
        return;
      }
      this.saving.set(true);
      this.service
        .create({
          email: result.email,
          password: result.password,
          fullName: result.fullName,
          role: result.role,
        })
        .pipe(finalize(() => this.saving.set(false)))
        .subscribe({
          next: () => {
            this.snack.open('Usuário cadastrado.', 'Fechar', {
              duration: 3000,
              panelClass: 'success-snack',
            });
            this.load();
          },
          error: () => undefined,
        });
    });
  }

  openEdit(user: User): void {
    this.openDialog(user).subscribe((result) => {
      if (result?.mode !== 'edit') {
        return;
      }
      this.saving.set(true);
      this.service
        .update(user.id, { fullName: result.fullName, role: result.role })
        .pipe(finalize(() => this.saving.set(false)))
        .subscribe({
          next: () => {
            this.snack.open('Usuário atualizado.', 'Fechar', {
              duration: 3000,
              panelClass: 'success-snack',
            });
            this.load();
          },
          error: () => undefined,
        });
    });
  }

  remove(user: User): void {
    this.dialog
      .open(ConfirmDialog, {
        data: {
          title: 'Remover usuário',
          message: `Remover ${user.fullName}? O acesso será revogado imediatamente.`,
          confirmLabel: 'Remover',
        },
      })
      .afterClosed()
      .subscribe((confirmed) => {
        if (!confirmed) {
          return;
        }
        this.saving.set(true);
        this.service
          .remove(user.id)
          .pipe(finalize(() => this.saving.set(false)))
          .subscribe({
            next: () => {
              this.snack.open('Usuário removido.', 'Fechar', {
                duration: 3000,
                panelClass: 'success-snack',
              });
              this.load();
            },
            error: () => undefined,
          });
      });
  }

  private openDialog(user?: User) {
    return this.dialog
      .open(UserFormDialog, {
        data: { user },
        width: '520px',
        maxWidth: '95vw',
      })
      .afterClosed();
  }
}
