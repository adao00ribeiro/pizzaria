import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { finalize } from 'rxjs';

import { Combo } from '../../core/models/pricing.models';
import { Size } from '../../core/models/product.models';
import { ConfirmDialog, ConfirmDialogData } from '../../shared/components/confirm-dialog';
import { BrlPipe } from '../../shared/pipes/brl.pipe';
import { ProductsService } from '../products/products.service';
import { ComboFormDialog, ComboFormDialogData } from './combo-form.dialog';
import { CombosService, ComboRequest } from './combos.service';

/**
 * Combos comerciais — docs/08 §3.8.
 * Lista os combos cadastrados e abre o montador com preview de margem.
 */
@Component({
  selector: 'pz-combos-page',
  standalone: true,
  imports: [
    RouterLink,
    MatCardModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatTooltipModule,
    MatDialogModule,
    MatSnackBarModule,
    MatProgressSpinnerModule,
    BrlPipe,
  ],
  templateUrl: './combos.page.html',
  styleUrl: './combos.page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CombosPage {
  private readonly service = inject(CombosService);
  private readonly productsService = inject(ProductsService);
  private readonly dialog = inject(MatDialog);
  private readonly snack = inject(MatSnackBar);

  readonly columns = ['name', 'discountValue', 'fixedPrice', 'items', 'actions'];
  readonly loading = signal(true);
  readonly combos = signal<Combo[]>([]);
  readonly sizes = signal<Size[]>([]);

  constructor() {
    this.load();
    this.productsService.sizes().subscribe({
      next: (items) => this.sizes.set(items),
      error: () => undefined,
    });
  }

  load(): void {
    this.loading.set(true);
    this.service
      .list()
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (items) => this.combos.set(items),
        error: () => undefined,
      });
  }

  itemSummary(combo: Combo): string {
    return combo.items
      .map((it) => {
        const name = it.productName ?? 'Produto';
        const size = it.sizeName ? ` (${it.sizeName})` : '';
        return `${it.quantity}x ${name}${size}`;
      })
      .join(', ');
  }

  openForm(combo?: Combo): void {
    const data: ComboFormDialogData = { combo, sizes: this.sizes() };
    this.dialog
      .open<ComboFormDialog, ComboFormDialogData, ComboRequest>(ComboFormDialog, {
        data,
        width: 'min(720px, 96vw)',
        autoFocus: 'first',
      })
      .afterClosed()
      .subscribe((body) => {
        if (!body) {
          return;
        }
        const request$ = combo ? this.service.update(combo.id, body) : this.service.create(body);
        request$.subscribe({
          next: () => {
            this.snack.open('Combo salvo com sucesso.', 'Fechar', {
              duration: 3000,
              panelClass: 'success-snack',
            });
            this.load();
          },
          error: () => undefined,
        });
      });
  }

  remove(combo: Combo): void {
    const data: ConfirmDialogData = {
      title: 'Excluir combo',
      message: `Excluir o combo "${combo.name}"?`,
      confirmLabel: 'Excluir',
    };
    this.dialog
      .open(ConfirmDialog, { data })
      .afterClosed()
      .subscribe((confirmed: boolean | undefined) => {
        if (!confirmed) {
          return;
        }
        this.service.remove(combo.id).subscribe({
          next: () => {
            this.snack.open('Combo excluído.', 'Fechar', {
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
