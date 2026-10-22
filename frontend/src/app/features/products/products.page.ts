import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { finalize } from 'rxjs';

import { ProductCategory } from '../../core/models/common.models';
import { CATEGORY_LABELS } from '../../core/models/labels';
import { Product, ProductRequest, Size, SizeRequest } from '../../core/models/product.models';
import { ConfirmDialog, ConfirmDialogData } from '../../shared/components/confirm-dialog';
import { ProductFormDialog, ProductFormDialogData } from './product-form.dialog';
import { SizeFormDialog, SizeFormDialogData } from './size-form.dialog';
import { ProductsService } from './products.service';
import { NonNullableFormControl } from '../../core/forms.compat';

/**
 * Produtos (sabores) e tamanhos em abas — docs/08 §3.2.
 * A aba "Tamanhos" também é acessível por `/products?tab=sizes`.
 */
@Component({
  selector: 'pz-products-page',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatTableModule,
    MatTabsModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatChipsModule,
    MatTooltipModule,
    MatDialogModule,
    MatSnackBarModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './products.page.html',
  styleUrl: './products.page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProductsPage {
  private readonly service = inject(ProductsService);
  private readonly dialog = inject(MatDialog);
  private readonly snack = inject(MatSnackBar);
  private readonly route = inject(ActivatedRoute);

  readonly categoryLabels = CATEGORY_LABELS;
  readonly categories: (ProductCategory | '')[] = ['', 'Pizza', 'Beverage', 'Other'];

  categoryLabel(category: ProductCategory): string {
    return CATEGORY_LABELS[category];
  }
  readonly productColumns = ['name', 'category', 'status', 'actions'];
  readonly sizeColumns = ['name', 'diameterCm', 'actions'];

  onThumbError(event: Event): void {
    const target = event.target as HTMLElement | null;
    if (target) {
      target.style.display = 'none';
    }
  }

  readonly activeTab = signal(0);
  readonly category = signal<ProductCategory | ''>('');
  readonly search = signal('');
  readonly searchControl = new NonNullableFormControl('');

  readonly productsLoading = signal(true);
  readonly products = signal<Product[]>([]);
  readonly sizesLoading = signal(true);
  readonly sizes = signal<Size[]>([]);

  /** Filtro client-side sobre a página carregada (docs/08 §3.3). */
  readonly filteredProducts = computed(() => {
    const term = this.search().toLowerCase();
    const list = this.products();
    if (!term) {
      return list;
    }
    return list.filter((p) => p.name.toLowerCase().includes(term));
  });

  constructor() {
    if (this.route.snapshot.queryParamMap.get('tab') === 'sizes') {
      this.activeTab.set(1);
    }
    this.searchControl.valueChanges
      .pipe(takeUntilDestroyed())
      .subscribe((term) => this.search.set(term.trim()));
    this.loadProducts();
    this.loadSizes();
  }

  onTab(index: number): void {
    this.activeTab.set(index);
  }

  onCategory(category: ProductCategory | ''): void {
    this.category.set(category);
    this.loadProducts();
  }

  loadProducts(): void {
    this.productsLoading.set(true);
    this.service
      .products(this.category())
      .pipe(finalize(() => this.productsLoading.set(false)))
      .subscribe({
        next: (items) => this.products.set(items),
        error: () => undefined,
      });
  }

  loadSizes(): void {
    this.sizesLoading.set(true);
    this.service
      .sizes()
      .pipe(finalize(() => this.sizesLoading.set(false)))
      .subscribe({
        next: (items) => this.sizes.set(items),
        error: () => undefined,
      });
  }

  openProduct(product?: Product): void {
    this.dialog
      .open<ProductFormDialog, ProductFormDialogData, ProductRequest>(ProductFormDialog, {
        data: { product },
        width: 'min(520px, 96vw)',
        autoFocus: 'first',
      })
      .afterClosed()
      .subscribe((body) => {
        if (!body) {
          return;
        }
        const request$ = product
          ? this.service.updateProduct(product.id, body)
          : this.service.createProduct(body);
        request$.subscribe({
          next: () => {
            this.snack.open('Produto salvo com sucesso.', 'Fechar', {
              duration: 3000,
              panelClass: 'success-snack',
            });
            this.loadProducts();
          },
          error: () => undefined,
        });
      });
  }

  removeProduct(product: Product): void {
    const data: ConfirmDialogData = {
      title: 'Excluir produto',
      message: `Excluir o produto "${product.name}"? Fichas técnicas e combos que o utilizam podem ser afetados.`,
      confirmLabel: 'Excluir',
    };
    this.dialog
      .open(ConfirmDialog, { data })
      .afterClosed()
      .subscribe((confirmed: boolean | undefined) => {
        if (!confirmed) {
          return;
        }
        this.service.removeProduct(product.id).subscribe({
          next: () => {
            this.snack.open('Produto excluído.', 'Fechar', {
              duration: 3000,
              panelClass: 'success-snack',
            });
            this.loadProducts();
          },
          error: (err: HttpErrorResponse) => {
            if (err.status === 409) {
              const body = err.error as { message?: string } | null;
              this.snack.open(
                body?.message ?? 'Este produto está em uso e não pode ser excluído.',
                'Fechar',
                { duration: 6000, panelClass: 'warn-snack' },
              );
            }
          },
        });
      });
  }

  openSize(size?: Size): void {
    this.dialog
      .open<SizeFormDialog, SizeFormDialogData, SizeRequest>(SizeFormDialog, {
        data: { size },
        width: 'min(520px, 96vw)',
        autoFocus: 'first',
      })
      .afterClosed()
      .subscribe((body) => {
        if (!body) {
          return;
        }
        const request$ = size
          ? this.service.updateSize(size.id, body)
          : this.service.createSize(body);
        request$.subscribe({
          next: () => {
            this.snack.open('Tamanho salvo com sucesso.', 'Fechar', {
              duration: 3000,
              panelClass: 'success-snack',
            });
            this.loadSizes();
          },
          error: () => undefined,
        });
      });
  }

  removeSize(size: Size): void {
    const data: ConfirmDialogData = {
      title: 'Excluir tamanho',
      message: `Excluir o tamanho "${size.name}"? Fichas técnicas que o utilizam podem ser afetadas.`,
      confirmLabel: 'Excluir',
    };
    this.dialog
      .open(ConfirmDialog, { data })
      .afterClosed()
      .subscribe((confirmed: boolean | undefined) => {
        if (!confirmed) {
          return;
        }
        this.service.removeSize(size.id).subscribe({
          next: () => {
            this.snack.open('Tamanho excluído.', 'Fechar', {
              duration: 3000,
              panelClass: 'success-snack',
            });
            this.loadSizes();
          },
          error: (err: HttpErrorResponse) => {
            if (err.status === 409) {
              const body = err.error as { message?: string } | null;
              this.snack.open(
                body?.message ?? 'Este tamanho está em uso e não pode ser excluído.',
                'Fechar',
                { duration: 6000, panelClass: 'warn-snack' },
              );
            }
          },
        });
      });
  }
}
