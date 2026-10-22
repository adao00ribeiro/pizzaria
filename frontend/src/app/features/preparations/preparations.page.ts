import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { finalize } from 'rxjs';

import { PAGE_SIZE, PagedResult } from '../../core/models/common.models';
import { Preparation, PreparationRequest } from '../../core/models/preparation.models';
import { ConfirmDialog, ConfirmDialogData } from '../../shared/components/confirm-dialog';
import { BrlPipe } from '../../shared/pipes/brl.pipe';
import { PreparationEditorDialog } from './preparation-editor.dialog';
import { PreparationsService } from './preparations.service';

/** Preparos: listagem de receitas base com custo total e custo por unidade. */
@Component({
  selector: 'pz-preparations-page',
  standalone: true,
  imports: [
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatPaginatorModule,
    MatProgressSpinnerModule,
    MatDialogModule,
    MatSnackBarModule,
    BrlPipe,
  ],
  templateUrl: './preparations.page.html',
  styleUrl: './preparations.page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PreparationsPage {
  private readonly service = inject(PreparationsService);
  private readonly dialog = inject(MatDialog);
  private readonly snack = inject(MatSnackBar);

  readonly pageSize = PAGE_SIZE;
  readonly columns = ['name', 'yield', 'totalCost', 'unitCost', 'actions'];

  readonly loading = signal(true);
  readonly items = signal<Preparation[]>([]);
  readonly total = signal(0);
  readonly page = signal(1);

  constructor() {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.service
      .list(this.page())
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (res: PagedResult<Preparation>) => {
          this.items.set(res.items);
          this.total.set(res.total);
        },
        error: () => undefined,
      });
  }

  onPage(event: PageEvent): void {
    this.page.set(event.pageIndex + 1);
    this.load();
  }

  openEditor(preparation?: Preparation): void {
    if (!preparation) {
      this.showEditor(undefined);
      return;
    }

    // A listagem não traz a composição: busca o detalhe para editar sem perder itens.
    this.service.get(preparation.id).subscribe({
      next: (full) => this.showEditor(full),
      error: () => this.showEditor(preparation),
    });
  }

  private showEditor(preparation?: Preparation): void {
    this.dialog
      .open<PreparationEditorDialog, { preparation?: Preparation }, PreparationRequest | undefined>(
        PreparationEditorDialog,
        {
          data: { preparation },
          width: 'min(820px, 96vw)',
          autoFocus: 'first',
        },
      )
      .afterClosed()
      .subscribe((body) => {
        if (!body) {
          return;
        }
        const request$ = preparation
          ? this.service.update(preparation.id, body)
          : this.service.create(body);
        request$.subscribe({
          next: () => {
            this.snack.open('Preparo salvo com sucesso.', 'Fechar', {
              duration: 3000,
              panelClass: 'success-snack',
            });
            this.load();
          },
          error: () => undefined,
        });
      });
  }

  remove(preparation: Preparation): void {
    const data: ConfirmDialogData = {
      title: 'Excluir preparo',
      message: `Excluir o preparo "${preparation.name}"? Receitas e fichas que o utilizam serão afetadas.`,
      confirmLabel: 'Excluir',
    };
    this.dialog
      .open(ConfirmDialog, { data })
      .afterClosed()
      .subscribe((confirmed: boolean | undefined) => {
        if (!confirmed) {
          return;
        }
        this.service.remove(preparation.id).subscribe({
          next: () => {
            this.snack.open('Preparo excluído.', 'Fechar', {
              duration: 3000,
              panelClass: 'success-snack',
            });
            this.load();
          },
          error: (err: HttpErrorResponse) => {
            if (err.status === 409) {
              const message =
                (err.error as { message?: string } | null)?.message ??
                'Este preparo está em uso e não pode ser excluído.';
              this.snack.open(message, 'Fechar', { duration: 6000, panelClass: 'error-snack' });
            }
          },
        });
      });
  }
}
