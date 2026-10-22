import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTableModule } from '@angular/material/table';
import { Chart, ChartConfiguration, ChartOptions, registerables } from 'chart.js';
import { BaseChartDirective } from 'ng2-charts';
import { finalize, forkJoin } from 'rxjs';

import {
  ProfitByChannelDto,
  ProfitByDayDto,
  ProfitByProductDto,
  ProfitReportDto,
} from '../../core/models/profit.models';
import { BrlPipe } from '../../shared/pipes/brl.pipe';
import { PctPipe } from '../../shared/pipes/pct.pipe';
import { ProfitService } from './profit.service';

Chart.register(...registerables);

export type PeriodPreset = 'today' | '7d' | '30d' | 'custom';

@Component({
  selector: 'pz-profit-page',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatCardModule,
    MatButtonModule,
    MatButtonToggleModule,
    MatFormFieldModule,
    MatInputModule,
    MatIconModule,
    MatTableModule,
    MatProgressSpinnerModule,
    BaseChartDirective,
    BrlPipe,
    PctPipe,
  ],
  templateUrl: './profit.page.html',
  styleUrl: './profit.page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProfitPage {
  private readonly service = inject(ProfitService);

  readonly loading = signal(true);
  readonly preset = signal<PeriodPreset>('30d');
  readonly fromCtrl = new FormControl('', { nonNullable: true });
  readonly toCtrl = new FormControl('', { nonNullable: true });

  readonly report = signal<ProfitReportDto | null>(null);
  readonly byDay = signal<ProfitByDayDto[]>([]);
  readonly byProduct = signal<ProfitByProductDto[]>([]);
  readonly byChannel = signal<ProfitByChannelDto[]>([]);

  readonly productColumns = ['productName', 'quantitySold', 'revenue', 'cost', 'grossProfit', 'marginPercentage'];
  readonly channelColumns = ['channelLabel', 'ordersCount', 'revenue', 'cost', 'grossProfit', 'marginPercentage', 'share'];

  readonly chartType = 'bar' as const;
  readonly chartOptions: ChartOptions<'bar'> = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: { legend: { display: true } },
    scales: {
      y: { beginAtZero: true },
    },
  };

  readonly chartData = (): ChartConfiguration<'bar'>['data'] => {
    const days = this.byDay();
    return {
      labels: days.map((d) => d.date.slice(5)),
      datasets: [
        {
          label: 'Faturamento (R$)',
          data: days.map((d) => d.revenue),
          backgroundColor: 'rgba(25, 118, 210, 0.75)',
          borderRadius: 4,
        },
        {
          label: 'Custo insumos (R$)',
          data: days.map((d) => d.cost),
          backgroundColor: 'rgba(245, 124, 0, 0.75)',
          borderRadius: 4,
        },
        {
          label: 'Lucro bruto (R$)',
          data: days.map((d) => d.grossProfit),
          backgroundColor: 'rgba(46, 125, 50, 0.85)',
          borderRadius: 4,
        },
      ],
    };
  };

  constructor() {
    this.selectPreset('30d');
  }

  selectPreset(p: PeriodPreset): void {
    this.preset.set(p);
    const today = new Date();
    const toStr = this.formatIsoDate(today);

    if (p === 'today') {
      this.fromCtrl.setValue(toStr);
      this.toCtrl.setValue(toStr);
      this.reload();
    } else if (p === '7d') {
      const from = new Date(today);
      from.setDate(today.getDate() - 6);
      this.fromCtrl.setValue(this.formatIsoDate(from));
      this.toCtrl.setValue(toStr);
      this.reload();
    } else if (p === '30d') {
      const from = new Date(today);
      from.setDate(today.getDate() - 29);
      this.fromCtrl.setValue(this.formatIsoDate(from));
      this.toCtrl.setValue(toStr);
      this.reload();
    }
  }

  reload(): void {
    const from = this.fromCtrl.value;
    const to = this.toCtrl.value;
    this.loading.set(true);

    forkJoin({
      summary: this.service.summary(from, to),
      byDay: this.service.byDay(from, to),
      byProduct: this.service.byProduct(from, to),
      byChannel: this.service.byChannel(from, to),
    })
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: ({ summary, byDay, byProduct, byChannel }) => {
          this.report.set(summary);
          this.byDay.set(byDay);
          this.byProduct.set(byProduct);
          this.byChannel.set(byChannel);
        },
        error: () => undefined,
      });
  }

  private formatIsoDate(d: Date): string {
    const yyyy = d.getFullYear();
    const mm = String(d.getMonth() + 1).padStart(2, '0');
    const dd = String(d.getDate()).padStart(2, '0');
    return `${yyyy}-${mm}-${dd}`;
  }
}
