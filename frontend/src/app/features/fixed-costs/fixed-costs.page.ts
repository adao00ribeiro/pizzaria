import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { finalize } from 'rxjs';

import { AllocationType, PAGE_SIZE, PagedResult } from '../../core/models/common.models';
import { ALLOCATION_LABELS } from '../../core/models/labels';
import { FixedCost, FixedCostRequest } from '../../core/models/cost.models';
import { ConfirmDialog, ConfirmDialogData } from '../../shared/components/confirm-dialog';
import { BrlPipe } from '../../shared/pipes/brl.pipe';
import { FixedCostFormDialog } from './fixed-cost-form.dialog';
import { FixedCostsService } from './fixed-costs.service';

/** Custos fixos: aluguel, energia, equipe — com rateio por pizza ou % do faturamento. */
@Component({
  selector: 'pz-fixed-costs-page',
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
  templateUrl: './fixed-costs.page.html',
  styleUrl: './fixed-costs.page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FixedCostsPage {
  private readonly service = inject(FixedCostsService);
  private readonly dialog = inject(MatDialog);
  private readonly snack = inject(MatSnackBar);

  readonly pageSize = PAGE_SIZE;
  readonly columns = ['description', 'allocation', 'monthlyValue', 'actions'];
  readonly allocationLabels = ALLOCATION_LABELS;

  allocationLabel(type: AllocationType): string {
    return ALLOCATION_LABELS[type];
  }

  readonly loading = signal(true);
  readonly items = signal<FixedCost[]>([]);
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
        next: (res: PagedResult<FixedCost>) => {
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

  monthlyTotal(): number {
    return this.items().reduce((acc, item) => acc + Number(item.monthlyValue ?? 0), 0);
  }

  openForm(fixedCost?: FixedCost): void {
    this.dialog
      .open<FixedCostFormDialog, { fixedCost?: FixedCost }, FixedCostRequest | undefined>(
        FixedCostFormDialog,
        {
          data: { fixedCost },
          width: 'min(620px, 96vw)',
          autoFocus: 'first',
        },
      )
      .afterClosed()
      .subscribe((body) => {
        if (!body) {
          return;
        }
        const request$ = fixedCost
          ? this.service.update(fixedCost.id, body)
          : this.service.create(body);
        request$.subscribe({
          next: () => {
            this.snack.open('Custo fixo salvo com sucesso.', 'Fechar', {
              duration: 3000,
              panelClass: 'success-snack',
            });
            this.load();
          },
          error: () => undefined,
        });
      });
  }

  remove(fixedCost: FixedCost): void {
    const data: ConfirmDialogData = {
      title: 'Excluir custo fixo',
      message: `Excluir o custo fixo "${fixedCost.description}"? O rateio deixará de entrar no custo indireto.`,
      confirmLabel: 'Excluir',
    };
    this.dialog
      .open(ConfirmDialog, { data })
      .afterClosed()
      .subscribe((confirmed: boolean | undefined) => {
        if (!confirmed) {
          return;
        }
        this.service.remove(fixedCost.id).subscribe({
          next: () => {
            this.snack.open('Custo fixo excluído.', 'Fechar', {
              duration: 3000,
              panelClass: 'success-snack',
            });
            this.load();
          },
          error: () => undefined,
        });
      });
  }
}
