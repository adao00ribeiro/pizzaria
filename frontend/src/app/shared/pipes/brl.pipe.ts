import { Pipe, PipeTransform } from '@angular/core';

/**
 * Pipe de moeda: `{{ 13.94 | brl }}` → `R$ 13,94` (docs/08 §5).
 * Nulos e indefinidos viram "—".
 */
@Pipe({ name: 'brl', standalone: true, pure: true })
export class BrlPipe implements PipeTransform {
  private static readonly formatter = new Intl.NumberFormat('pt-BR', {
    style: 'currency',
    currency: 'BRL',
  });

  transform(value: number | null | undefined): string {
    if (value === null || value === undefined || Number.isNaN(Number(value))) {
      return '—';
    }
    return BrlPipe.formatter.format(Number(value));
  }
}
