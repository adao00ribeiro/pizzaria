import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { DatePipe } from '@angular/common';
import { ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { combineLatest, debounceTime, distinctUntilChanged, finalize, startWith } from 'rxjs';

import { AlertStatus, AlertType, PAGE_SIZE } from '../../core/models/common.models';
import { ALERT_STATUS_LABELS, ALERT_TYPE_LABELS } from '../../core/models/labels';
import { Alert } from '../../core/models/alert.models';
import { AlertsService } from './alerts.service';
import { NonNullableFormControl } from '../../core/forms.compat';
import { ReplenishStockDialog, ReplenishStockDialogData } from '../ingredients/replenish-stock.dialog';

/**
 * Alertas de margem e estoque — docs/08 §3.10.
 * Filtros por status/tipo e ação "Marcar resolvido".
 */
@Component({
  selector: 'pz-alerts-page',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatTableModule,
    MatFormFieldModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatChipsModule,
    MatPaginatorModule,
    MatProgressSpinnerModule,
    MatTooltipModule,
    MatDialogModule,
    MatSnackBarModule,
    DatePipe,
  ],
  templateUrl: './alerts.page.html',
  styleUrl: './alerts.page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AlertsPage {
  private readonly service = inject(AlertsService);
  private readonly dialog = inject(MatDialog);
  private readonly snack = inject(MatSnackBar);

  readonly pageSize = PAGE_SIZE;
  readonly columns = ['type', 'message', 'createdAt', 'status', 'actions'];
  readonly typeLabels = ALERT_TYPE_LABELS;
  readonly statusLabels = ALERT_STATUS_LABELS;

  typeLabel(type: AlertType): string {
    return ALERT_TYPE_LABELS[type];
  }

  statusLabel(status: AlertStatus): string {
    return ALERT_STATUS_LABELS[status];
  }

  readonly loading = signal(true);
  readonly alerts = signal<Alert[]>([]);
  readonly total = signal(0);
  readonly page = signal(1);
  readonly resolvingId = signal('');

  readonly statusControl = new NonNullableFormControl<AlertStatus | ''>('');
  readonly typeControl = new NonNullableFormControl<AlertType | ''>('');

  readonly filters = combineLatest([
    this.statusControl.valueChanges.pipe(startWith('' as AlertStatus | '')),
    this.typeControl.valueChanges.pipe(startWith('' as AlertType | '')),
  ]).pipe(debounceTime(0), distinctUntilChanged());

  constructor() {
    // A primeira emissão (startWith) carrega a página inicial.
    this.filters.pipe(takeUntilDestroyed()).subscribe(() => {
      this.page.set(1);
      this.load();
    });
  }

  load(): void {
    this.loading.set(true);
    this.service
      .list({
        page: this.page(),
        status: this.statusControl.value,
        type: this.typeControl.value,
      })
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (res) => {
          this.alerts.set(res.items);
          this.total.set(res.total);
        },
        error: () => undefined,
      });
  }

  onPage(event: PageEvent): void {
    this.page.set(event.pageIndex + 1);
    this.load();
  }

  openReplenish(alert: Alert): void {
    // Tenta extrair o nome do ingrediente informado entre aspas simples na mensagem:
    // Ex: "Estoque de 'Muçarela' abaixo do mínimo (-0.86 kg < 1 kg)."
    const match = alert.message.match(/'([^']+)'/);
    const ingredientName = match ? match[1] : undefined;

    this.dialog
      .open<ReplenishStockDialog, ReplenishStockDialogData, boolean>(
        ReplenishStockDialog,
        {
          data: { ingredientName },
          width: 'min(620px, 96vw)',
          autoFocus: 'first',
        },
      )
      .afterClosed()
      .subscribe((saved) => {
        if (saved) {
          this.load();
        }
      });
  }

  resolve(alert: Alert): void {
    this.resolvingId.set(alert.id);
    this.service
      .resolve(alert.id)
      .pipe(finalize(() => this.resolvingId.set('')))
      .subscribe({
        next: () => {
          this.snack.open('Alerta marcado como resolvido.', 'Fechar', {
            duration: 3000,
            panelClass: 'success-snack',
          });
          this.load();
        },
        error: () => undefined,
      });
  }
}
