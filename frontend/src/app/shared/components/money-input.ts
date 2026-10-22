import { Directive, ElementRef, inject, forwardRef } from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';

/** Formata um número em moeda brasileira para exibição (R$ 32,90). */
export function formatBrl(value: number | null | undefined): string {
  if (value === null || value === undefined || Number.isNaN(value)) {
    return '';
  }
  return new Intl.NumberFormat('pt-BR', {
    style: 'currency',
    currency: 'BRL',
  }).format(value);
}

/** Converte texto digitado pelo usuário (aceita "32,90", "1.234,56", "32.9") em número. */
export function parseMoney(text: string): number | null {
  const trimmed = (text ?? '').trim();
  if (!trimmed) {
    return null;
  }
  let s = trimmed.replace(/[^\d.,]/g, '');
  if (s.includes(',')) {
    s = s.replace(/\./g, '').replace(',', '.');
  }
  if (!s || s === '.') {
    return null;
  }
  const n = parseFloat(s);
  return Number.isNaN(n) ? null : n;
}

/**
 * Input de valor monetário (MoneyInput — docs/08 §5):
 * `matInput` com formatação de moeda e `parseFloat` seguro.
 * Uso: `<input matInput pzMoney formControlName="purchasePrice">`
 * O valor no FormControl é sempre `number | null`; a formatação R$ aparece
 * ao sair do campo, e o cálculo ao vivo acontece a cada digitação.
 */
@Directive({
  selector: 'input[pzMoney]',
  standalone: true,
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => MoneyInputDirective),
      multi: true,
    },
  ],
  host: {
    '(input)': 'onInput($event)',
    '(blur)': 'onBlur()',
    '(focus)': 'onFocus()',
  },
})
export class MoneyInputDirective implements ControlValueAccessor {
  private readonly el = inject<ElementRef<HTMLInputElement>>(ElementRef);

  private onChange: (value: number | null) => void = () => {};
  private onTouched: () => void = () => {};

  writeValue(value: number | null | undefined): void {
    this.el.nativeElement.value = formatBrl(
      value === null || value === undefined || Number.isNaN(Number(value)) ? null : Number(value),
    );
  }

  registerOnChange(fn: (value: number | null) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(isDisabled: boolean): void {
    this.el.nativeElement.disabled = isDisabled;
  }

  onInput(event: Event): void {
    const text = (event.target as HTMLInputElement).value;
    this.onChange(parseMoney(text));
  }

  onBlur(): void {
    const input = this.el.nativeElement;
    const value = parseMoney(input.value);
    input.value = formatBrl(value);
    this.onTouched();
  }

  onFocus(): void {
    this.el.nativeElement.select();
  }
}
