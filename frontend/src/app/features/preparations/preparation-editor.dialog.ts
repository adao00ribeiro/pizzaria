import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  FormArray,
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { MatAutocompleteModule, MatAutocompleteSelectedEvent } from '@angular/material/autocomplete';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { catchError, debounceTime, finalize, forkJoin, of, switchMap } from 'rxjs';

import { UNITS, SheetItemType, Unit } from '../../core/models/common.models';
import { UNIT_LABELS } from '../../core/models/labels';
import {
  Preparation,
  PreparationCalculation,
  PreparationItemRequest,
  PreparationRequest,
} from '../../core/models/preparation.models';
import { Ingredient } from '../../core/models/ingredient.models';
import { IngredientsService } from '../ingredients/ingredients.service';
import { PreparationsService } from './preparations.service';
import { BrlPipe } from '../../shared/pipes/brl.pipe';
import { NonNullableFormControl, NonNullableFormGroup } from '../../core/forms.compat';

/** Opção unificada do autocomplete: ingrediente OU preparo (docs/08 §3.4). */
export interface RecipeOption {
  key: string;
  id: string;
  type: SheetItemType;
  label: string;
  unit: Unit;
  search: string;
}

interface ItemRowControls {
  ref: FormControl<RecipeOption | null>;
  quantity: FormControl<number | null>;
  unit: FormControl<Unit>;
}

export interface PreparationDialogData {
  preparation?: Preparation;
}

/**
 * Montador de receita: nome, rendimento, lista dinâmica de itens com
 * autocomplete (ingrediente ou preparo) e custo ao vivo via
 * POST /preparations/calculate-preview com debounce de 300 ms (docs/08 §3.4).
 */
@Component({
  selector: 'pz-preparation-editor-dialog',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatAutocompleteModule,
    MatIconModule,
    MatProgressBarModule,
    BrlPipe,
  ],
  templateUrl: './preparation-editor.dialog.html',
  styleUrl: './preparation-editor.dialog.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PreparationEditorDialog {
  readonly data = inject<PreparationDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(
    MatDialogRef<PreparationEditorDialog, PreparationRequest>,
  );
  private readonly ingredientsService = inject(IngredientsService);
  private readonly preparationsService = inject(PreparationsService);

  readonly units: Unit[] = [...UNITS];
  readonly unitLabels = UNIT_LABELS;
  readonly editing = !!this.data.preparation;

  readonly options = signal<RecipeOption[]>([]);
  readonly ready = signal(false);
  readonly loadWarning = signal<string | null>(null);
  readonly calculating = signal(false);
  readonly preview = signal<PreparationCalculation | null>(null);
  readonly previewError = signal<string | null>(null);
  readonly itemsHint = signal<string | null>(null);
  readonly filters = signal<Record<number, string>>({});

  readonly items = new FormArray<FormGroup<ItemRowControls>>([]);

  readonly form = new NonNullableFormGroup({
    name: new NonNullableFormControl(this.data.preparation?.name ?? '', [Validators.required]),
    yieldQuantity: new FormControl<number | null>(this.data.preparation?.yieldQuantity ?? null, [
      Validators.required,
      Validators.min(0.0001),
    ]),
    yieldUnit: new NonNullableFormControl<Unit>(this.data.preparation?.yieldUnit ?? 'un', [
      Validators.required,
    ]),
    items: this.items,
  });

  constructor() {
    forkJoin({
      ingredients: this.ingredientsService.list({ page: 1, pageSize: 1000 }),
      preparations: this.preparationsService.list(),
    }).subscribe({
      next: ({ ingredients, preparations }) => {
        this.options.set(this.buildOptions(ingredients.items, preparations.items));
        this.ready.set(true);
        this.applyExisting();
      },
      error: () => {
        this.ready.set(true);
        this.loadWarning.set(
          'Não foi possível carregar a base de ingredientes/preparos. Verifique sua conexão.',
        );
      },
    });

    // Cálculo ao vivo com debounce de 300 ms, cancelando requisições antigas
    this.form.valueChanges
      .pipe(
        debounceTime(300),
        switchMap(() => this.previewRequest()),
        takeUntilDestroyed(),
      )
      .subscribe((calc) => {
        this.preview.set(calc);
        if (calc) {
          this.previewError.set(null);
        }
      });
  }

  displayOption(option: RecipeOption | null): string {
    return option ? option.label : '';
  }

  filteredOptions(index: number): RecipeOption[] {
    const term = this.filters()[index] ?? '';
    const excludeId = this.data.preparation?.id;
    const base = this.options().filter((o) => o.id !== excludeId || o.type !== 'Preparation');
    if (!term) {
      return base;
    }
    return base.filter((o) => o.search.includes(term));
  }

  setFilter(index: number, value: string): void {
    const term = (value ?? '').trim().toLowerCase();
    this.filters.update((f) => ({ ...f, [index]: term }));
  }

  onOptionSelected(index: number, event: MatAutocompleteSelectedEvent): void {
    const option = event.option.value as RecipeOption | null;
    if (option) {
      this.itemAt(index).controls.unit.setValue(option.unit);
    }
    this.filters.update((f) => ({ ...f, [index]: '' }));
  }

  addItem(): void {
    this.items.push(this.newItemRow());
    this.itemsHint.set(null);
  }

  removeItem(index: number): void {
    this.items.removeAt(index);
    this.filters.update((f) => {
      const next: Record<number, string> = {};
      for (const key of Object.keys(f).map(Number)) {
        if (key < index) {
          next[key] = f[key];
        } else if (key > index) {
          next[key - 1] = f[key];
        }
      }
      return next;
    });
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const rows = this.items.controls as FormGroup<ItemRowControls>[];
    if (rows.length === 0) {
      this.itemsHint.set('Adicione ao menos um item à receita.');
      return;
    }
    const hasInvalidRow = rows.some((row) => {
      const qty = Number(row.controls.quantity.value ?? 0);
      return !row.controls.ref.value || !(qty > 0);
    });
    if (hasInvalidRow) {
      rows.forEach((row) => row.markAllAsTouched());
      this.itemsHint.set('Cada item precisa ter ingrediente/preparo e quantidade maior que zero.');
      return;
    }
    this.dialogRef.close(this.buildRequest());
  }

  private itemAt(index: number): FormGroup<ItemRowControls> {
    return this.items.at(index) as FormGroup<ItemRowControls>;
  }

  private newItemRow(ref?: RecipeOption, quantity?: number, unit?: Unit): FormGroup<ItemRowControls> {
    return new FormGroup<ItemRowControls>({
      ref: new FormControl<RecipeOption | null>(ref ?? null),
      quantity: new FormControl<number | null>(quantity ?? null, [Validators.min(0.0001)]),
      unit: new FormControl<Unit>(unit ?? 'g', { nonNullable: true }),
    });
  }

  private buildOptions(ingredients: Ingredient[], preparations: Preparation[]): RecipeOption[] {
    const ingOptions: RecipeOption[] = ingredients.map((i) => ({
      key: `Ingredient:${i.id}`,
      id: i.id,
      type: 'Ingredient' as const,
      label: i.name,
      unit: i.purchaseUnit,
      search: i.name.toLowerCase(),
    }));
    const prepOptions: RecipeOption[] = preparations.map((p) => ({
      key: `Preparation:${p.id}`,
      id: p.id,
      type: 'Preparation' as const,
      label: p.name,
      unit: p.yieldUnit,
      search: p.name.toLowerCase(),
    }));
    return [...ingOptions, ...prepOptions];
  }

  /** Pré-carrega a receita existente quando a listagem devolve a composição. */
  private applyExisting(): void {
    const preparation = this.data.preparation;
    if (!preparation) {
      return;
    }
    const existing = preparation.items ?? [];
    if (existing.length === 0) {
      this.loadWarning.set(
        'A composição atual desta receita não está disponível para edição. Salvar substituirá os itens pelos que você montar aqui.',
      );
      return;
    }
    let incomplete = false;
    for (const item of existing) {
      const option = this.findOption(item);
      if (!option) {
        incomplete = true;
        continue;
      }
      this.items.push(this.newItemRow(option, item.quantity, item.unit));
    }
    if (incomplete) {
      this.loadWarning.set(
        'Nem todos os itens da receita puderam ser recarregados. Revise antes de salvar: o que não aparecer aqui será substituído.',
      );
    }
  }

  private findOption(item: {
    type: SheetItemType;
    ingredientId?: string | null;
    preparationId?: string | null;
    description?: string | null;
  }): RecipeOption | null | undefined {
    const wantedId = item.type === 'Ingredient' ? item.ingredientId : item.preparationId;
    if (wantedId) {
      const byId = this.options().find((o) => o.type === item.type && o.id === wantedId);
      if (byId) {
        return byId;
      }
    }
    const name = (item.description ?? '').toLowerCase();
    if (name) {
      return this.options().find((o) => o.type === item.type && o.search === name);
    }
    return null;
  }

  private buildRequest(): PreparationRequest {
    const rows = this.items.controls as FormGroup<ItemRowControls>[];
    const requestItems: PreparationItemRequest[] = [];
    for (const row of rows) {
      const ref = row.controls.ref.value;
      const quantity = Number(row.controls.quantity.value ?? 0);
      if (!ref || !(quantity > 0)) {
        continue;
      }
      const item: PreparationItemRequest = {
        type: ref.type,
        quantity,
        unit: row.controls.unit.value,
      };
      if (ref.type === 'Ingredient') {
        item.ingredientId = ref.id;
      } else {
        item.preparationId = ref.id;
      }
      requestItems.push(item);
    }
    return {
      name: this.form.controls.name.value.trim(),
      yieldQuantity: Number(this.form.controls.yieldQuantity.value ?? 0),
      yieldUnit: this.form.controls.yieldUnit.value,
      items: requestItems,
    };
  }

  private previewRequest() {
    const body = this.buildRequest();
    if (!(body.yieldQuantity > 0)) {
      this.previewError.set(null);
      return of(null);
    }
    this.calculating.set(true);
    return this.preparationsService.calculatePreview(body).pipe(
      finalize(() => this.calculating.set(false)),
      catchError(() => {
        this.previewError.set('Não foi possível calcular o custo agora. Verifique os itens.');
        return of(null);
      }),
    );
  }
}
