import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { ChartConfiguration, ChartType } from 'chart.js';
import { BaseChartDirective } from 'ng2-charts';

import { ChartKind, ChartSpec, ChartValueFormat } from '../../core/models/ai.models';

const PALETTE = [
  '#e53935', // Red / Pizza
  '#1e88e5', // Blue
  '#43a047', // Green
  '#fb8c00', // Orange
  '#8e24aa', // Purple
  '#00acc1', // Cyan
  '#fdd835', // Yellow
  '#6d4c41', // Brown
  '#546e7a', // Slate
  '#d81b60', // Pink
];

@Component({
  selector: 'pz-chart-block',
  standalone: true,
  imports: [BaseChartDirective],
  templateUrl: './chart-block.component.html',
  styleUrl: './chart-block.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ChartBlockComponent {
  readonly spec = input.required<ChartSpec>();

  readonly isCircular = computed(() => {
    const k = this.spec()?.kind;
    return k === 'pie' || k === 'doughnut';
  });

  readonly chartType = computed<ChartType>(() => {
    const k = this.spec()?.kind;
    switch (k) {
      case 'line':
        return 'line';
      case 'pie':
        return 'pie';
      case 'doughnut':
        return 'doughnut';
      case 'bar':
      default:
        return 'bar';
    }
  });

  readonly hasData = computed(() => {
    const s = this.spec();
    if (!s || !s.series || s.series.length === 0) return false;
    return s.series.some((serie) => serie.data && serie.data.length > 0);
  });

  readonly chartData = computed<ChartConfiguration['data']>(() => {
    const s = this.spec();
    if (!s) return { labels: [], datasets: [] };

    const circular = this.isCircular();

    if (circular) {
      const firstSeries = s.series[0] || { name: '', data: [] };
      return {
        labels: [...s.labels],
        datasets: [
          {
            label: firstSeries.name || s.title,
            data: [...firstSeries.data],
            backgroundColor: PALETTE.slice(0, s.labels.length),
            borderWidth: 1,
          },
        ],
      };
    }

    const datasets = s.series.map((serie, index) => {
      const color = PALETTE[index % PALETTE.length];
      const isLine = s.kind === 'line';

      return {
        label: serie.name,
        data: [...serie.data],
        backgroundColor: isLine ? `${color}33` : `${color}cc`,
        borderColor: color,
        borderWidth: 2,
        fill: isLine,
        tension: 0.3,
      };
    });

    return {
      labels: [...s.labels],
      datasets,
    };
  });

  readonly chartOptions = computed<ChartConfiguration['options']>(() => {
    const s = this.spec();
    const circular = this.isCircular();
    const format = s?.valueFormat || 'number';

    const formatValue = (val: number | string): string => {
      const num = typeof val === 'number' ? val : parseFloat(val);
      if (isNaN(num)) return String(val);

      switch (format) {
        case 'currency':
          return num.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });
        case 'percent':
          return `${num.toLocaleString('pt-BR', { minimumFractionDigits: 1, maximumFractionDigits: 1 })}%`;
        case 'number':
        default:
          return num.toLocaleString('pt-BR');
      }
    };

    const options: ChartConfiguration['options'] = {
      responsive: true,
      maintainAspectRatio: false,
      plugins: {
        legend: {
          display: true,
          position: circular ? 'bottom' : 'top',
          labels: {
            boxWidth: 14,
            usePointStyle: true,
            font: { family: 'inherit', size: 12 },
          },
        },
        tooltip: {
          callbacks: {
            label: (ctx) => {
              const label = ctx.dataset.label || '';
              const rawVal = ctx.parsed.y !== undefined ? ctx.parsed.y : ctx.parsed;
              const val = rawVal ?? 0;
              return `${label ? label + ': ' : ''}${formatValue(val)}`;
            },
          },
        },
      },
    };

    if (!circular) {
      options.scales = {
        y: {
          beginAtZero: true,
          ticks: {
            callback: (val) => formatValue(val as number),
            font: { family: 'inherit', size: 11 },
          },
          grid: {
            color: 'rgba(0, 0, 0, 0.06)',
          },
        },
        x: {
          grid: {
            display: false,
          },
          ticks: {
            font: { family: 'inherit', size: 11 },
          },
        },
      };
    }

    return options;
  });
}
