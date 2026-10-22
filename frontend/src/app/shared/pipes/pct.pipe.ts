import { Pipe, PipeTransform } from '@angular/core';

/**
 * Pipe de percentual: `{{ 60 | pct }}` → `60,0%` (docs/08 §5).
 * O valor chega da API já em percentual (60 = 60%).
 */
@Pipe({ name: 'pct', standalone: true, pure: true })
export class PctPipe implements PipeTransform {
  transform(value: number | null | undefined, digits = 1): string {
    if (value === null || value === undefined || Number.isNaN(Number(value))) {
      return '—';
    }
    return `${Number(value).toFixed(digits).replace('.', ',')}%`;
  }
}
