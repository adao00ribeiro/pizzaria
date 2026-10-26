import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';

import {
  NEIGHBORHOOD_REGION_LABELS,
  NeighborhoodDto,
  NeighborhoodRegion,
} from '../../core/models/neighborhood.models';
import { ConfirmDialog, ConfirmDialogData } from '../../shared/components/confirm-dialog';
import { NeighborhoodFormDialog } from './neighborhood-form.dialog';
import { NeighborhoodsService } from './neighborhoods.service';

@Component({
  selector: 'pz-neighborhoods-page',
  standalone: true,
  imports: [
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatTooltipModule,
  ],
  templateUrl: './neighborhoods.page.html',
  styleUrl: './neighborhoods.page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NeighborhoodsPage implements OnInit {
  private readonly service = inject(NeighborhoodsService);
  private readonly dialog = inject(MatDialog);

  readonly items = signal<NeighborhoodDto[]>([]);
  readonly loading = signal(true);
  readonly columns: readonly string[] = ['name', 'region', 'status', 'actions'];

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.loading.set(true);
    this.service.list().subscribe({
      next: (list) => {
        this.items.set(list);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
      },
    });
  }

  regionLabel(region: NeighborhoodRegion): string {
    return NEIGHBORHOOD_REGION_LABELS[region] || region;
  }

  openForm(neighborhood?: NeighborhoodDto): void {
    this.dialog
      .open(NeighborhoodFormDialog, {
        data: { neighborhood },
        width: '440px',
      })
      .afterClosed()
      .subscribe((result) => {
        if (result) {
          this.reload();
        }
      });
  }

  deleteItem(item: NeighborhoodDto): void {
    const data: ConfirmDialogData = {
      title: 'Excluir bairro',
      message: `Tem certeza de que deseja remover o bairro "${item.name}"?`,
      confirmLabel: 'Excluir',
      cancelLabel: 'Cancelar',
    };

    this.dialog
      .open(ConfirmDialog, { data })
      .afterClosed()
      .subscribe((confirmed) => {
        if (confirmed) {
          this.service.delete(item.id).subscribe({
            next: () => this.reload(),
          });
        }
      });
  }
}
