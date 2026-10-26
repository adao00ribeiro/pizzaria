import { ChangeDetectionStrategy, Component, input } from '@angular/core';

import { AnswerKpi } from '../../core/models/ai.models';

@Component({
  selector: 'pz-kpi-block',
  standalone: true,
  templateUrl: './kpi-block.component.html',
  styleUrl: './kpi-block.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KpiBlockComponent {
  readonly kpis = input.required<AnswerKpi[]>();

  formatKpi(kpi: AnswerKpi): string {
    const raw = kpi.value;
    const num = parseFloat(raw);

    if (isNaN(num)) return raw;

    switch (kpi.valueFormat) {
      case 'currency':
        return num.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });
      case 'percent':
        return `${num.toLocaleString('pt-BR', { minimumFractionDigits: 1, maximumFractionDigits: 1 })}%`;
      case 'number':
      default:
        // Se a string já tinha formatação prévia (ex: R$ ou % já inserido), mantém
        if (raw.includes('R$') || raw.includes('%')) return raw;
        return num.toLocaleString('pt-BR');
    }
  }
}
