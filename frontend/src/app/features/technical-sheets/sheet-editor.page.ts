import { HttpErrorResponse } from '@angular/common/http';
import { KeyValuePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormArray, FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatStepperModule } from '@angular/material/stepper';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import {
  BehaviorSubject,
  debounceTime,
  distinctUntilChanged,
  finalize,
  map,
  of,
  startWith,
  switchMap,
} from 'rxjs';

import { SheetItemType, Unit } from '../../core/models/common.models';
import { UNIT_LABELS } from '../../core/models/labels';
import { Ingredient } from '../../core/models/ingredient.models';
import { Preparation } from '../../core/models/preparation.models';
import { Product, Size } from '../../core/models/product.models';
import { PricingPreviewRequest, SheetDetail, SheetSaveRequest } from '../../core/models/sheet.models';
import { PricingResult } from '../../core/models/pricing.models';
import { extractApiErrorMessage } from '../../core/interceptors/error.interceptor';
import { MarginStatusChip } from '../../shared/components/margin-status-chip';
import { PricingResultCards } from '../../shared/components/pricing-result-cards';
import { BrlPipe } from '../../shared/pipes/brl.pipe';
import { IngredientsService } from '../ingredients/ingredients.service';
import { PreparationsService } from '../preparations/preparations.service';
import { ProductsService } from '../products/products.service';
import { PricingService } from '../pricing/pricing.service';
import { SheetsService } from './sheets.service';

/** Opção do autocomplete unificado (ingrediente OU preparo). */
interface ItemOption {
  key: string;
  label: string;
  name: string;
  type: SheetItemType;
  id: string;
  unit: Unit;
}

/** Recálculo em andamento: docs/08 §6 manda mostrar a barra indeterminada. */
const previewBusy$ = new BehaviorSubject<boolean>(false);

/**
 * Montador de ficha técnica — docs/08 §3.5.
 * Stepper ① produto/tamanho → ② itens → ③ custos → ④ resultado, com painel
 * lateral sticky atualizado ao vivo via `POST /pricing/preview` (debounce 300 ms).
 */
@Component({
  selector: 'pz-sheet-editor-page',
  standalone: true,
  imports: [
    RouterLink,
    ReactiveFormsModule,
    MatStepperModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatAutocompleteModule,
    MatButtonModule,
    MatIconModule,
    MatChipsModule,
    MatTableModule,
    MatTooltipModule,
    MatProgressBarModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
    MarginStatusChip,
    PricingResultCards,
    BrlPipe,
    KeyValuePipe,
  ],
  templateUrl: './sheet-editor.page.html',
  styleUrl: './sheet-editor.page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SheetEditorPage {
  private readonly sheetsService = inject(SheetsService);
  private readonly productsService = inject(ProductsService);
  private readonly ingredientsService = inject(IngredientsService);
  private readonly preparationsService = inject(PreparationsService);
  private readonly pricingService = inject(PricingService);
  private readonly snack = inject(MatSnackBar);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly fb = inject(FormBuilder);

  private readonly knownLabels = new Map<string, string>();

  readonly unitLabels = UNIT_LABELS;
  readonly itemColumns = ['label', 'quantity', 'unit', 'actions'];

  readonly sheetId = this.route.snapshot.paramMap.get('id');
  readonly isNew = this.sheetId === null;

  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly previewing = toSignal(previewBusy$, { initialValue: false });
  readonly result = signal<PricingResult | null>(null);
  readonly previewError = signal('');

  readonly products = signal<Product[]>([]);
  readonly sizes = signal<Size[]>([]);
  readonly ingredients = signal<Ingredient[]>([]);
  readonly preparations = signal<Preparation[]>([]);

  readonly filterTerm = signal('');

  readonly form = this.fb.group({
    productId: ['', [Validators.required]],
    sizeId: ['', [Validators.required]],
    packagingCost: [0, [Validators.required, Validators.min(0)]],
    lossPercentage: [0, [Validators.required, Validators.min(0), Validators.max(100)]],
    marginPercentage: [60, [Validators.required, Validators.min(0), Validators.max(100)]],
    items: this.fb.array([this.buildItemGroup()]),
  });

  get items(): FormArray<ReturnType<SheetEditorPage['buildItemGroup']>> {
    return this.form.controls.items;
  }

  private buildItemGroup(value: { ref: string; quantity: number; unit: string } = { ref: '', quantity: 0, unit: 'g' }) {
    return this.fb.nonNullable.group({
      ref: [value.ref, [Validators.required]],
      quantity: [value.quantity > 0 ? value.quantity : (null as any), [Validators.required, Validators.min(0.0001)]],
      unit: [value.unit || 'g', [Validators.required]],
    });
  }

  /** Catálogo unificado de ingredientes + preparos (imutável pelo filtro de digitação). */
  readonly allOptions = computed<ItemOption[]>(() => {
    return [
      ...this.ingredients().map((i: Ingredient) => ({
        key: `Ingredient:${i.id}`,
        label: `${i.name} (ingrediente)`,
        name: i.name,
        type: 'Ingredient' as SheetItemType,
        id: i.id,
        unit: i.purchaseUnit,
      })),
      ...this.preparations().map((p: Preparation) => ({
        key: `Preparation:${p.id}`,
        label: `${p.name} (preparo)`,
        name: p.name,
        type: 'Preparation' as SheetItemType,
        id: p.id,
        unit: p.yieldUnit,
      })),
    ];
  });

  /** Opções filtradas exibidas no popup do autocomplete. */
  readonly filteredOptions = computed(() => {
    const all = this.allOptions();
    const t = this.filterTerm().trim().toLowerCase();
    if (!t) return all;
    return all.filter((o) => o.label.toLowerCase().includes(t) || o.name.toLowerCase().includes(t));
  });

  constructor() {
    this.loadCatalogs();

    // Recálculo ao vivo com debounce (docs/08 §3.5).
    this.form.valueChanges
      .pipe(
        startWith(null),
        debounceTime(300),
        distinctUntilChanged((a, b) => JSON.stringify(a) === JSON.stringify(b)),
        switchMap(() => {
          const body = this.buildPreviewRequest();
          if (!body || body.items.length === 0) {
            this.previewError.set('');
            return of(null);
          }
          previewBusy$.next(true);
          return this.pricingService.preview(body).pipe(
            finalize(() => previewBusy$.next(false)),
          );
        }),
        takeUntilDestroyed(),
      )
      .subscribe({
        next: (res) => {
          if (res) {
            this.previewError.set('');
            this.result.set(res);
          }
        },
        error: (err: HttpErrorResponse) => {
          this.previewError.set(extractApiErrorMessage(err));
        },
      });
  }

  private loadCatalogs(): void {
    this.productsService.products().subscribe({
      next: (items) => this.products.set(items),
      error: () => undefined,
    });
    this.productsService.sizes().subscribe({
      next: (items) => this.sizes.set(items),
      error: () => undefined,
    });
    this.ingredientsService.list({ page: 1, pageSize: 200 }).subscribe({
      next: (res) => this.ingredients.set(res.items),
      error: () => undefined,
    });
    this.preparationsService.list(1).subscribe({
      next: (res) => this.preparations.set(res.items),
      error: () => undefined,
    });

    if (this.sheetId) {
      this.loadSheet(this.sheetId);
    }
  }

  private loadSheet(id: string): void {
    this.loading.set(true);
    this.sheetsService
      .get(id)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (sheet) => this.patchForm(sheet),
        error: () => undefined,
      });
  }

  private patchForm(sheet: SheetDetail): void {
    this.form.patchValue({
      productId: sheet.productId,
      sizeId: sheet.sizeId,
      packagingCost: sheet.packagingCost,
      lossPercentage: sheet.lossPercentage ?? 0,
      marginPercentage: sheet.marginPercentage ?? 60,
    });
    this.result.set(sheet.result);

    const arr = this.items;
    arr.clear();
    for (const item of sheet.items) {
      const ref =
        item.type === 'Ingredient'
          ? `Ingredient:${item.ingredientId}`
          : `Preparation:${item.preparationId}`;
      if (item.description) {
        this.knownLabels.set(ref, item.description);
      }
      arr.push(
        this.buildItemGroup({
          ref,
          quantity: item.quantity,
          unit: item.unit,
        }),
      );
    }
  }

  // -------------------------------------------------------------------------
  // Itens da ficha
  // -------------------------------------------------------------------------

  addItem(): void {
    this.items.push(this.buildItemGroup());
  }

  removeItem(index: number): void {
    this.items.removeAt(index);
  }

  onItemFilter(term: string): void {
    this.filterTerm.set(term);
  }

  onItemFocus(value: string): void {
    const opt = this.allOptions().find((o) => o.name === value || o.label === value || o.key === value);
    if (opt) {
      this.filterTerm.set('');
    } else {
      this.filterTerm.set(value ?? '');
    }
  }

  /** Ao escolher uma opção no autocomplete, ajusta a unidade padrão e guarda o label. */
  onOptionSelected(index: number, key: string): void {
    const opt = this.allOptions().find((o) => o.key === key);
    if (opt) {
      this.items.at(index).patchValue({
        ref: opt.key,
        unit: opt.unit,
      });
      this.knownLabels.set(opt.key, opt.name);
    }
  }

  /** `displayWith` do autocomplete: converte a chave em texto legível. */
  displayOption(value: string): string {
    if (!value) return '';
    const opt = this.allOptions().find((o) => o.key === value);
    if (opt) return opt.name;
    return this.knownLabels.get(value) ?? value;
  }

  // -------------------------------------------------------------------------
  // Validação dos passos
  // -------------------------------------------------------------------------

  step1Valid(): boolean {
    return this.form.controls.productId.valid && this.form.controls.sizeId.valid;
  }

  step2Valid(): boolean {
    return this.items.length > 0 && this.items.valid;
  }

  step3Valid(): boolean {
    return (
      this.form.controls.packagingCost.valid &&
      this.form.controls.lossPercentage.valid &&
      this.form.controls.marginPercentage.valid
    );
  }

  // -------------------------------------------------------------------------
  // Preview / salvar
  // -------------------------------------------------------------------------

  private buildPreviewRequest(): PricingPreviewRequest | null {
    const { productId, sizeId, packagingCost, lossPercentage, marginPercentage } = this.form.getRawValue();
    if (!productId || !sizeId) {
      return null;
    }
    const items = this.items
      .getRawValue()
      .filter((it) => !!it.ref && Number(it.quantity) > 0)
      .map((it) => {
        // A chave pode vir do autocomplete ("Ingredient:<id>") ou do nome
        // recarregado de uma ficha existente — nos dois casos resolvemos abaixo.
        const [maybeType, maybeId] = it.ref.split(':');
        const type: SheetItemType =
          maybeType === 'Ingredient' || maybeType === 'Preparation'
            ? maybeType
            : this.resolveType(it.ref);
        const id =
          type === 'Ingredient'
            ? maybeType === 'Ingredient'
              ? maybeId
              : this.ingredients().find((i) => i.name === it.ref)?.id
            : maybeType === 'Preparation'
              ? maybeId
              : this.preparations().find((p) => p.name === it.ref)?.id;
        return {
          type,
          ingredientId: type === 'Ingredient' ? id : undefined,
          preparationId: type === 'Preparation' ? id : undefined,
          quantity: Number(it.quantity),
          unit: it.unit as Unit,
        };
      });
    return {
      productId,
      sizeId,
      packagingCost: Number(packagingCost ?? 0),
      lossPercentage: Number(lossPercentage ?? 0),
      marginPercentage: Number(marginPercentage ?? 0),
      items,
    };
  }

  private resolveType(ref: string): SheetItemType {
    return this.ingredients().some((i) => i.name === ref) ? 'Ingredient' : 'Preparation';
  }

  save(): void {
    const body = this.buildPreviewRequest();
    if (!body || !this.step1Valid() || !this.step2Valid() || !this.step3Valid()) {
      this.snack.open('Preencha todos os passos antes de salvar.', 'Fechar', {
        duration: 4000,
        panelClass: 'warn-snack',
      });
      return;
    }
    const request$ = this.sheetId
      ? this.sheetsService.update(this.sheetId, body as SheetSaveRequest)
      : this.sheetsService.create(body as SheetSaveRequest);
    this.saving.set(true);
    request$.pipe(finalize(() => this.saving.set(false))).subscribe({
      next: () => {
        this.snack.open('Ficha técnica salva com sucesso.', 'Fechar', {
          duration: 4000,
          panelClass: 'success-snack',
        });
        this.router.navigate(['/sheets']);
      },
      error: () => undefined,
    });
  }
}
