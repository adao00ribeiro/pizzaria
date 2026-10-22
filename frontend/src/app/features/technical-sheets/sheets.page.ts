import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { finalize } from 'rxjs';

import { MarginStatus, PAGE_SIZE } from '../../core/models/common.models';
import { SheetListItem } from '../../core/models/sheet.models';
import { ConfirmDialog, ConfirmDialogData } from '../../shared/components/confirm-dialog';
import { MarginStatusChip } from '../../shared/components/margin-status-chip';
import { BrlPipe } from '../../shared/pipes/brl.pipe';
import { PctPipe } from '../../shared/pipes/pct.pipe';
import { SheetsService } from './sheets.service';

type StatusFilter = MarginStatus | '';

/**
 * Listagem de fichas técnicas precificadas — docs/08 §3.6.
 * Filtro por status (chips Todos/Vermelhos/Verdes) + ordenação por coluna.
 */
@Component({
  selector: 'pz-sheets-page',
  standalone: true,
  imports: [
    RouterLink,
    MatTableModule,
    MatSortModule,
    MatPaginatorModule,
    MatButtonModule,
    MatIconModule,
    MatChipsModule,
    MatTooltipModule,
    MatDialogModule,
    MatSnackBarModule,
    MatProgressBarModule,
    MatProgressSpinnerModule,
    MarginStatusChip,
    BrlPipe,
    PctPipe,
  ],
  templateUrl: './sheets.page.html',
  styleUrl: './sheets.page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SheetsPage {
  private readonly service = inject(SheetsService);
  private readonly dialog = inject(MatDialog);
  private readonly snack = inject(MatSnackBar);

  readonly pageSize = PAGE_SIZE;
  readonly columns = ['productName', 'sizeName', 'directCost', 'suggestedPrice', 'estimatedProfit', 'marginRealPercentage', 'marginStatus', 'actions'];

  readonly loading = signal(true);
  readonly items = signal<SheetListItem[]>([]);
  readonly total = signal(0);
  readonly page = signal(1);
  readonly statusFilter = signal<StatusFilter>('');
  readonly sort = signal<Sort>({ active: '', direction: '' });

  /** Ordenação client-side sobre a página carregada. */
  readonly sortedItems = computed(() => {
    const { active, direction } = this.sort();
    const list = this.items();
    if (!active || !direction) {
      return list;
    }
    const col = active as keyof SheetListItem;
    const dir = direction === 'desc' ? -1 : 1;
    return [...list].sort((a, b) => {
      const av = a[col];
      const bv = b[col];
      if (typeof av === 'number' && typeof bv === 'number') {
        return (av - bv) * dir;
      }
      return String(av ?? '').localeCompare(String(bv ?? ''), 'pt-BR') * dir;
    });
  });

  constructor() {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.service
      .list({ page: this.page(), status: this.statusFilter() })
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (res) => {
          this.items.set(res.items);
          this.total.set(res.total);
        },
        error: () => undefined,
      });
  }

  applyStatus(status: StatusFilter): void {
    this.statusFilter.set(status);
    this.page.set(1);
    this.load();
  }

  onPage(event: PageEvent): void {
    this.page.set(event.pageIndex + 1);
    this.load();
  }

  onSort(sort: Sort): void {
    this.sort.set(sort);
  }

  remove(sheet: SheetListItem): void {
    const data: ConfirmDialogData = {
      title: 'Excluir ficha técnica',
      message: `Excluir a ficha de "${sheet.productName}" (${sheet.sizeName})? O item voltará a aparecer como não precificado.`,
      confirmLabel: 'Excluir',
    };
    this.dialog
      .open(ConfirmDialog, { data })
      .afterClosed()
      .subscribe((confirmed: boolean | undefined) => {
        if (!confirmed) {
          return;
        }
        this.service.remove(sheet.id).subscribe({
          next: () => {
            this.snack.open('Ficha técnica excluída.', 'Fechar', {
              duration: 3000,
              panelClass: 'success-snack',
            });
            this.load();
          },
          error: (err: HttpErrorResponse) => {
            if (err.status === 409) {
              const body = err.error as { message?: string } | null;
              this.snack.open(
                body?.message ?? 'Esta ficha não pode ser excluída.',
                'Fechar',
                { duration: 6000, panelClass: 'warn-snack' },
              );
            }
          },
        });
      });
  }
}
