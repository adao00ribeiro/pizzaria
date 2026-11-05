import { CommonModule, DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { finalize } from 'rxjs';

import { ConfirmDialog } from '../../../shared/components/confirm-dialog';
import { SystemAdminService, TenantListItem } from '../../../core/services/system-admin.service';
import { DeactivateTenantDialog } from './deactivate-tenant.dialog';

@Component({
  selector: 'pz-tenants-list',
  standalone: true,
  imports: [
    CommonModule,
    DatePipe,
    ReactiveFormsModule,
    MatTableModule,
    MatPaginatorModule,
    MatButtonModule,
    MatIconModule,
    MatChipsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatSnackBarModule,
    MatProgressSpinnerModule,
    MatTooltipModule,
  ],
  template: `
    <div class="tenants-view">
      <!-- Título e Estatísticas -->
      <div class="page-title-row">
        <div>
          <h1 class="page-title">Pizzarias Cadastradas</h1>
          <p class="subtitle">Visibilidade e moderação centralizada de todos os estabelecimentos</p>
        </div>
        <div class="stats-badge">
          <mat-icon>storefront</mat-icon>
          <span>Total: <strong>{{ totalItems() }}</strong></span>
        </div>
      </div>

      <!-- Filtros e Busca -->
      <div class="filter-card">
        <mat-form-field appearance="outline" class="search-field">
          <mat-label>Buscar por nome ou slug...</mat-label>
          <input
            matInput
            [formControl]="searchControl"
            (keyup.enter)="applyFilters()"
          />
          <mat-icon matPrefix>search</mat-icon>
          @if (searchControl.value) {
            <button mat-icon-button matSuffix (click)="clearSearch()">
              <mat-icon>close</mat-icon>
            </button>
          }
        </mat-form-field>

        <mat-form-field appearance="outline" class="status-field">
          <mat-label>Status Operacional</mat-label>
          <mat-select [formControl]="statusControl" (selectionChange)="applyFilters()">
            <mat-option value="all">Todos</mat-option>
            <mat-option value="active">Ativos</mat-option>
            <mat-option value="inactive">Desativados</mat-option>
          </mat-select>
        </mat-form-field>

        <button mat-flat-button color="primary" class="filter-btn" (click)="applyFilters()">
          <mat-icon>filter_list</mat-icon>
          Filtrar
        </button>
      </div>

      <!-- Tabela -->
      <div class="table-container mat-elevation-z1">
        @if (loading()) {
          <div class="loading-overlay">
            <mat-spinner diameter="44"></mat-spinner>
          </div>
        }

        <table mat-table [dataSource]="tenants()" class="tenants-table">
          <!-- Nome -->
          <ng-container matColumnDef="name">
            <th mat-header-cell *matHeaderCellDef>Pizzaria</th>
            <td mat-cell *matCellDef="let item">
              <div class="tenant-name-col">
                <strong class="tenant-name">{{ item.name }}</strong>
                <span class="tenant-id">ID: {{ item.id }}</span>
              </div>
            </td>
          </ng-container>

          <!-- Slug -->
          <ng-container matColumnDef="slug">
            <th mat-header-cell *matHeaderCellDef>Slug Público</th>
            <td mat-cell *matCellDef="let item">
              @if (item.slug) {
                <span class="slug-chip">/{{ item.slug }}</span>
              } @else {
                <span class="empty-field">—</span>
              }
            </td>
          </ng-container>

          <!-- Responsável -->
          <ng-container matColumnDef="owner">
            <th mat-header-cell *matHeaderCellDef>Responsável</th>
            <td mat-cell *matCellDef="let item">
              <div class="owner-col">
                <span class="owner-name">{{ item.ownerName }}</span>
                <span class="owner-email">{{ item.ownerEmail }}</span>
              </div>
            </td>
          </ng-container>

          <!-- Data de Criação -->
          <ng-container matColumnDef="createdAt">
            <th mat-header-cell *matHeaderCellDef>Criado em</th>
            <td mat-cell *matCellDef="let item" class="created-col">
              {{ item.createdAt | date: 'dd/MM/yyyy HH:mm' }}
            </td>
          </ng-container>

          <!-- Status -->
          <ng-container matColumnDef="status">
            <th mat-header-cell *matHeaderCellDef>Status</th>
            <td mat-cell *matCellDef="let item">
              @if (item.isActive) {
                <span class="badge badge-active">
                  <mat-icon>check_circle</mat-icon>
                  Ativo
                </span>
              } @else {
                <span
                  class="badge badge-inactive"
                  [matTooltip]="getDeactivationTooltip(item)"
                  matTooltipPosition="above"
                >
                  <mat-icon>block</mat-icon>
                  Desativado
                </span>
              }
            </td>
          </ng-container>

          <!-- Ações -->
          <ng-container matColumnDef="actions">
            <th mat-header-cell *matHeaderCellDef class="actions-header">Ações</th>
            <td mat-cell *matCellDef="let item" class="actions-col">
              @if (item.isActive) {
                <button
                  mat-stroked-button
                  color="warn"
                  class="action-btn"
                  (click)="deactivateTenant(item)"
                  matTooltip="Suspender acesso e cardápio imediatamente"
                >
                  <mat-icon>pause_circle_outline</mat-icon>
                  Desativar
                </button>
              } @else {
                <button
                  mat-flat-button
                  color="primary"
                  class="action-btn"
                  (click)="reactivateTenant(item)"
                  matTooltip="Restabelecer operações do estabelecimento"
                >
                  <mat-icon>play_circle_outline</mat-icon>
                  Reativar
                </button>
              }
            </td>
          </ng-container>

          <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
          <tr mat-row *matRowDef="let row; columns: displayedColumns"></tr>

          <!-- Estado Vazio -->
          <tr class="mat-row" *matNoDataRow>
            <td class="mat-cell empty-table" [attr.colspan]="displayedColumns.length">
              Nenhuma pizzaria encontrada com os filtros informados.
            </td>
          </tr>
        </table>

        <mat-paginator
          class="custom-paginator"
          [length]="totalItems()"
          [pageSize]="pageSize()"
          [pageIndex]="pageIndex()"
          [pageSizeOptions]="[10, 15, 25, 50]"
          (page)="onPageChange($event)"
          showFirstLastButtons
        ></mat-paginator>
      </div>
    </div>
  `,
  styles: [`
    .tenants-view {
      max-width: 1300px;
      margin: 0 auto;
    }

    .page-title-row {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      margin-bottom: 1.5rem;
      gap: 1rem;
      flex-wrap: wrap;

      .page-title {
        font-size: 1.75rem;
        font-weight: 800;
        margin: 0;
        color: var(--pz-text-main, #0f172a);
        letter-spacing: -0.02em;
      }

      .subtitle {
        color: var(--pz-text-muted, #64748b);
        margin-top: 0.35rem;
        font-size: 0.92rem;
      }

      .stats-badge {
        display: flex;
        align-items: center;
        gap: 0.6rem;
        background: var(--pz-bg-card, #ffffff);
        border: 1px solid var(--pz-border, #e2e8f0);
        padding: 0.6rem 1.1rem;
        border-radius: 10px;
        font-weight: 500;
        color: var(--pz-text-main, #0f172a);
        box-shadow: var(--pz-shadow-card, 0 1px 3px rgba(0, 0, 0, 0.04));

        mat-icon {
          color: var(--pz-primary, #c62828);
          font-size: 20px;
          width: 20px;
          height: 20px;
        }

        strong {
          color: var(--pz-primary, #c62828);
          font-weight: 700;
        }
      }
    }

    .filter-card {
      display: flex;
      gap: 1rem;
      align-items: center;
      background: var(--pz-bg-card, #ffffff);
      border: 1px solid var(--pz-border, #e2e8f0);
      padding: 1rem 1.25rem;
      border-radius: 12px;
      margin-bottom: 1.5rem;
      flex-wrap: wrap;
      box-shadow: var(--pz-shadow-card, 0 1px 3px rgba(0, 0, 0, 0.04));

      .search-field {
        flex: 1;
        min-width: 260px;
        margin-bottom: -1.25em;
      }

      .status-field {
        width: 190px;
        margin-bottom: -1.25em;
      }

      .filter-btn {
        height: 52px;
        padding: 0 1.5rem;
        font-weight: 600;
        border-radius: 8px;
      }
    }

    .table-container {
      position: relative;
      background: var(--pz-bg-card, #ffffff);
      border: 1px solid var(--pz-border, #e2e8f0);
      border-radius: 12px;
      overflow: hidden;
      box-shadow: var(--pz-shadow-card, 0 1px 3px rgba(0, 0, 0, 0.04));
    }

    .loading-overlay {
      position: absolute;
      top: 0;
      left: 0;
      right: 0;
      bottom: 0;
      background: rgba(var(--pz-bg-app), 0.7);
      backdrop-filter: blur(2px);
      display: flex;
      align-items: center;
      justify-content: center;
      z-index: 10;
    }

    .tenants-table {
      width: 100%;
      background: transparent;

      th {
        color: var(--pz-text-secondary, #475569);
        font-weight: 700;
        font-size: 0.8rem;
        text-transform: uppercase;
        letter-spacing: 0.05em;
        border-bottom: 1px solid var(--pz-border, #e2e8f0);
        background: var(--pz-bg-muted, #f1f5f9);
        padding: 14px 16px;
      }

      td {
        border-bottom: 1px solid var(--pz-border-subtle, #f1f5f9);
        color: var(--pz-text-main, #0f172a);
        padding: 14px 16px;
      }

      tr.mat-mdc-row:hover {
        background-color: var(--pz-bg-card-hover, #f8fafc);
      }
    }

    .tenant-name-col {
      display: flex;
      flex-direction: column;

      .tenant-name {
        font-size: 0.95rem;
        color: var(--pz-text-main, #0f172a);
      }

      .tenant-id {
        font-size: 0.72rem;
        color: var(--pz-text-muted, #64748b);
        font-family: monospace;
        margin-top: 2px;
      }
    }

    .slug-chip {
      display: inline-block;
      background: var(--pz-bg-muted, #f1f5f9);
      padding: 0.25rem 0.6rem;
      border-radius: 6px;
      font-family: monospace;
      font-size: 0.84rem;
      color: var(--pz-primary, #c62828);
      border: 1px solid var(--pz-border, #e2e8f0);
    }

    .empty-field {
      color: var(--pz-text-muted, #64748b);
    }

    .owner-col {
      display: flex;
      flex-direction: column;

      .owner-name {
        font-weight: 600;
        font-size: 0.9rem;
        color: var(--pz-text-main, #0f172a);
      }

      .owner-email {
        font-size: 0.8rem;
        color: var(--pz-text-muted, #64748b);
        margin-top: 2px;
      }
    }

    .created-col {
      font-size: 0.88rem;
      color: var(--pz-text-secondary, #475569);
    }

    .badge {
      display: inline-flex;
      align-items: center;
      gap: 0.3rem;
      padding: 0.25rem 0.7rem;
      border-radius: 20px;
      font-size: 0.8rem;
      font-weight: 700;

      mat-icon {
        font-size: 15px;
        width: 15px;
        height: 15px;
      }

      &.badge-active {
        background: var(--pz-success-light, #ecfdf5);
        color: var(--pz-success, #059669);
        border: 1px solid rgba(5, 150, 105, 0.3);
      }

      &.badge-inactive {
        background: var(--pz-danger-light, #fef2f2);
        color: var(--pz-danger, #dc2626);
        border: 1px solid rgba(220, 38, 38, 0.3);
        cursor: help;
      }
    }

    .actions-header {
      text-align: right;
    }

    .actions-col {
      text-align: right;
    }

    .action-btn {
      font-weight: 600;
      border-radius: 6px;

      mat-icon {
        margin-right: 0.25rem;
        font-size: 18px;
        width: 18px;
        height: 18px;
      }
    }

    .empty-table {
      text-align: center;
      padding: 3rem !important;
      color: var(--pz-text-muted, #64748b);
      font-style: italic;
    }

    .custom-paginator {
      background-color: var(--pz-bg-card, #ffffff) !important;
      color: var(--pz-text-secondary, #475569) !important;
      border-top: 1px solid var(--pz-border, #e2e8f0) !important;
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TenantsListPage implements OnInit {
  readonly adminService = inject(SystemAdminService);
  private readonly dialog = inject(MatDialog);
  private readonly snack = inject(MatSnackBar);

  readonly displayedColumns = ['name', 'slug', 'owner', 'createdAt', 'status', 'actions'];

  readonly tenants = signal<TenantListItem[]>([]);
  readonly totalItems = signal(0);
  readonly loading = signal(false);

  readonly pageIndex = signal(0);
  readonly pageSize = signal(15);

  readonly searchControl = new FormControl('', { nonNullable: true });
  readonly statusControl = new FormControl('all', { nonNullable: true });

  ngOnInit(): void {
    this.loadTenants();
  }

  loadTenants(): void {
    this.loading.set(true);

    const search = this.searchControl.value.trim() || undefined;
    const statusVal = this.statusControl.value;
    let isActive: boolean | undefined = undefined;
    if (statusVal === 'active') isActive = true;
    else if (statusVal === 'inactive') isActive = false;

    this.adminService
      .getTenants(this.pageIndex() + 1, this.pageSize(), search, isActive)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (result) => {
          this.tenants.set(result.items ?? []);
          this.totalItems.set(result.total ?? 0);
        },
        error: (err) => {
          this.snack.open(
            err.error?.message || 'Falha ao carregar lista de pizzarias.',
            'Fechar',
            { duration: 5000, panelClass: 'error-snack' }
          );
        },
      });
  }

  applyFilters(): void {
    this.pageIndex.set(0);
    this.loadTenants();
  }

  clearSearch(): void {
    this.searchControl.setValue('');
    this.applyFilters();
  }

  onPageChange(event: PageEvent): void {
    this.pageIndex.set(event.pageIndex);
    this.pageSize.set(event.pageSize);
    this.loadTenants();
  }

  deactivateTenant(tenant: TenantListItem): void {
    const dialogRef = this.dialog.open(DeactivateTenantDialog, {
      width: '500px',
      data: { tenantName: tenant.name },
    });

    dialogRef.afterClosed().subscribe((reason?: string) => {
      if (!reason) return;

      this.loading.set(true);
      this.adminService
        .updateTenantStatus(tenant.id, { isActive: false, reason })
        .pipe(finalize(() => this.loading.set(false)))
        .subscribe({
          next: (updated) => {
            this.snack.open(
              `Pizzaria "${updated.name}" foi desativada com corte imediato.`,
              'OK',
              { duration: 5000 }
            );
            this.updateLocalTenant(updated);
          },
          error: (err) => {
            this.snack.open(
              err.error?.message || 'Falha ao desativar pizzaria.',
              'Fechar',
              { duration: 5000, panelClass: 'error-snack' }
            );
          },
        });
    });
  }

  reactivateTenant(tenant: TenantListItem): void {
    const dialogRef = this.dialog.open(ConfirmDialog, {
      data: {
        title: 'Reativar Pizzaria',
        message: `Deseja restabelecer imediatamente as operações de "${tenant.name}"? O acesso dos colaboradores e o cardápio público serão reativados.`,
        confirmLabel: 'Reativar',
        cancelLabel: 'Cancelar',
      },
    });

    dialogRef.afterClosed().subscribe((confirmed?: boolean) => {
      if (!confirmed) return;

      this.loading.set(true);
      this.adminService
        .updateTenantStatus(tenant.id, { isActive: true })
        .pipe(finalize(() => this.loading.set(false)))
        .subscribe({
          next: (updated) => {
            this.snack.open(
              `Pizzaria "${updated.name}" foi reativada com sucesso!`,
              'OK',
              { duration: 5000 }
            );
            this.updateLocalTenant(updated);
          },
          error: (err) => {
            this.snack.open(
              err.error?.message || 'Falha ao reativar pizzaria.',
              'Fechar',
              { duration: 5000, panelClass: 'error-snack' }
            );
          },
        });
    });
  }

  getDeactivationTooltip(tenant: TenantListItem): string {
    const date = tenant.deactivatedAt
      ? new Date(tenant.deactivatedAt).toLocaleString('pt-BR')
      : '';
    const reason = tenant.deactivationReason ? ` - Motivo: ${tenant.deactivationReason}` : '';
    return `Desativado em ${date}${reason}`;
  }

  private updateLocalTenant(updated: TenantListItem): void {
    this.tenants.update((list) =>
      list.map((item) => (item.id === updated.id ? updated : item))
    );
  }
}
