import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { forkJoin, finalize } from 'rxjs';

import { MarginStatus } from '../../core/models/common.models';
import { RankingItem, DashboardSummary, MarginChartPoint } from '../../core/models/pricing.models';
import { SheetDetail } from '../../core/models/sheet.models';
import { Product, Size } from '../../core/models/product.models';
import { MarginStatusChip } from '../../shared/components/margin-status-chip';
import { PricingResultCards } from '../../shared/components/pricing-result-cards';
import { BrlPipe } from '../../shared/pipes/brl.pipe';
import { PctPipe } from '../../shared/pipes/pct.pipe';
import { ProductsService } from '../products/products.service';
import { DashboardService } from './dashboard.service';

import { Chart, ChartConfiguration, ChartOptions, registerables } from 'chart.js';
import { BaseChartDirective } from 'ng2-charts';
import { NonNullableFormControl, NonNullableFormGroup } from '../../core/forms.compat';

Chart.register(...registerables);

/**
 * Dashboard Executivo: indicadores estratégicos de alto padrão,
 * rankings comparativos de lucro e custo com miniaturas e barras relativas,
 * gráfico moderno de margem por sabor e simulador de ficha técnica em tempo real.
 */
@Component({
  selector: 'pz-dashboard-page',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatChipsModule,
    MatTooltipModule,
    MatSelectModule,
    MatTableModule,
    MatProgressSpinnerModule,
    MarginStatusChip,
    PricingResultCards,
    BrlPipe,
    PctPipe,
    BaseChartDirective,
  ],
  templateUrl: './dashboard.page.html',
  styleUrl: './dashboard.page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DashboardPage {
  private readonly dashboard = inject(DashboardService);
  private readonly productsService = inject(ProductsService);

  readonly loading = signal(true);
  readonly summary = signal<DashboardSummary | null>(null);
  readonly profitRanking = signal<RankingItem[]>([]);
  readonly costRanking = signal<RankingItem[]>([]);
  readonly margins = signal<MarginChartPoint[]>([]);

  readonly pizzaProducts = signal<Product[]>([]);
  readonly sizes = signal<Size[]>([]);
  readonly individual = signal<SheetDetail | null>(null);
  readonly individualLoading = signal(false);

  readonly individualForm = new NonNullableFormGroup({
    productId: new NonNullableFormControl(''),
    sizeId: new NonNullableFormControl(''),
  });

  /** Maior valor de lucro no ranking para cálculo de proporção relativa (%) */
  readonly maxProfit = computed(() => {
    const items = this.profitRanking();
    if (!items || items.length === 0) return 1;
    const max = Math.max(...items.map((i) => i.value));
    return max > 0 ? max : 1;
  });

  /** Maior valor de custo no ranking para cálculo de proporção relativa (%) */
  readonly maxCost = computed(() => {
    const items = this.costRanking();
    if (!items || items.length === 0) return 1;
    const max = Math.max(...items.map((i) => i.value));
    return max > 0 ? max : 1;
  });

  /** Mapeamento otimizado de nome de produto para miniatura/imagem */
  private readonly productMap = computed(() => {
    const map = new Map<string, Product>();
    for (const prod of this.pizzaProducts()) {
      map.set(prod.name.trim().toLowerCase(), prod);
    }
    return map;
  });

  getProductImage(productName: string): string | null {
    if (!productName) return null;
    const prod = this.productMap().get(productName.trim().toLowerCase());
    return prod?.imageUrl || null;
  }

  readonly chartType = 'bar' as const;
  readonly chartOptions: ChartOptions<'bar'> = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: { display: false },
      tooltip: {
        backgroundColor: '#1e293b',
        titleFont: { size: 13, weight: 'bold' },
        bodyFont: { size: 12 },
        padding: 10,
        cornerRadius: 8,
        callbacks: {
          label: (context) => ` Margem Real: ${Number(context.raw).toFixed(1)}%`,
        },
      },
    },
    scales: {
      y: {
        beginAtZero: true,
        grid: {
          color: 'rgba(0, 0, 0, 0.05)',
        },
        ticks: {
          color: '#64748b',
          callback: (value: string | number) => `${value}%`,
        },
      },
      x: {
        grid: {
          display: false,
        },
        ticks: {
          color: '#475569',
          autoSkip: false,
          maxRotation: 45,
          minRotation: 0,
          font: {
            size: 11,
          },
        },
      },
    },
  };

  readonly chartData = (): ChartConfiguration<'bar'>['data'] => {
    const points = this.margins();
    return {
      labels: points.map((p) => p.name),
      datasets: [
        {
          label: 'Margem %',
          data: points.map((p) => p.marginPercentage),
          backgroundColor: points.map((p) =>
            p.marginPercentage >= 40
              ? 'rgba(16, 185, 129, 0.85)' // verde esmeralda para margens saudáveis
              : p.marginPercentage >= 20
              ? 'rgba(245, 158, 11, 0.85)' // âmbar para margens medianas
              : 'rgba(239, 68, 68, 0.85)'  // vermelho para atenção
          ),
          borderColor: points.map((p) =>
            p.marginPercentage >= 40
              ? '#059669'
              : p.marginPercentage >= 20
              ? '#d97706'
              : '#dc2626'
          ),
          borderWidth: 1.5,
          borderRadius: 6,
          hoverBackgroundColor: '#8e1a1a',
        },
      ],
    };
  };

  constructor() {
    this.reload();

    forkJoin({
      products: this.productsService.products('Pizza'),
      sizes: this.productsService.sizes(),
    }).subscribe({
      next: ({ products, sizes }) => {
        this.pizzaProducts.set(products);
        this.sizes.set(sizes);
      },
      error: () => undefined,
    });

    this.individualForm.valueChanges
      .pipe(takeUntilDestroyed())
      .subscribe(({ productId, sizeId }) => {
        if (!productId || !sizeId) {
          this.individual.set(null);
          return;
        }
        this.individualLoading.set(true);
        this.dashboard.individual(productId, sizeId).subscribe({
          next: (detail) => {
            this.individual.set(detail);
            this.individualLoading.set(false);
          },
          error: () => {
            this.individual.set(null);
            this.individualLoading.set(false);
          },
        });
      });
  }

  reload(): void {
    this.loading.set(true);
    forkJoin({
      summary: this.dashboard.summary(),
      profit: this.dashboard.rankings('profit', 'desc'),
      cost: this.dashboard.rankings('cost', 'desc'),
      margins: this.dashboard.marginsChart(),
    })
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: ({ summary, profit, cost, margins }) => {
          this.summary.set(summary);
          this.profitRanking.set(profit.items);
          this.costRanking.set(cost.items);
          this.margins.set(margins);
        },
        error: () => undefined,
      });
  }
}
