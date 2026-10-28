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
import { forkJoin, finalize, of, catchError } from 'rxjs';

import { MarginStatus } from '../../core/models/common.models';
import { RankingItem, DashboardSummary, MarginChartPoint } from '../../core/models/pricing.models';
import { SheetDetail } from '../../core/models/sheet.models';
import { Product, Size } from '../../core/models/product.models';
import { ProfitByProductDto } from '../../core/models/profit.models';
import { MarginStatusChip } from '../../shared/components/margin-status-chip';
import { PricingResultCards } from '../../shared/components/pricing-result-cards';
import { BrlPipe } from '../../shared/pipes/brl.pipe';
import { PctPipe } from '../../shared/pipes/pct.pipe';
import { ProductsService } from '../products/products.service';
import { DashboardService } from './dashboard.service';
import { ProfitService } from '../profit/profit.service';

import { Chart, ChartConfiguration, ChartOptions, registerables } from 'chart.js';
import { BaseChartDirective } from 'ng2-charts';
import { NonNullableFormControl, NonNullableFormGroup } from '../../core/forms.compat';

Chart.register(...registerables);

export type ChartViewMode = 'highlights' | 'top10' | 'bottom10' | 'all';
export type RankingSource = 'sales' | 'sheet';
export type ChartSource = 'sales' | 'sheet';

export interface ChartMarginPoint {
  name: string;
  marginPercentage: number;
  revenue?: number;
  cost?: number;
  grossProfit?: number;
  quantitySold?: number;
}

/**
 * Dashboard Executivo: indicadores estratégicos de alto padrão,
 * rankings de vendas reais (pedidos faturados) e fichas técnicas unitárias,
 * gráfico moderno de margem por sabor em barras horizontais com filtros executivos
 * e simulador de ficha técnica individual estruturado em blocos lógicos.
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
  private readonly profitService = inject(ProfitService);

  readonly loading = signal(true);
  readonly summary = signal<DashboardSummary | null>(null);
  readonly profitRanking = signal<RankingItem[]>([]);
  readonly costRanking = signal<RankingItem[]>([]);
  readonly margins = signal<MarginChartPoint[]>([]);

  // Origem dos rankings e do gráfico: 'sales' (vendas reais dos pedidos) ou 'sheet' (ficha técnica unitária)
  readonly rankingSource = signal<RankingSource>('sales');
  readonly chartSource = signal<ChartSource>('sales');
  readonly realSalesProducts = signal<ProfitByProductDto[]>([]);

  // Controles de limites para rankings
  readonly salesQtyLimit = signal<number>(5);
  readonly salesProfitLimit = signal<number>(5);
  readonly profitLimit = signal<number>(5);
  readonly costLimit = signal<number>(5);

  // Filtros e controles do gráfico de margens
  readonly chartMode = signal<ChartViewMode>('highlights');
  readonly chartSizeFilter = signal<string>('all');

  readonly totalChartItems = computed(() => {
    return this.chartSource() === 'sales'
      ? this.realSalesProducts().length
      : this.margins().length;
  });

  readonly pizzaProducts = signal<Product[]>([]);
  readonly sizes = signal<Size[]>([]);
  readonly individual = signal<SheetDetail | null>(null);
  readonly individualLoading = signal(false);

  readonly individualForm = new NonNullableFormGroup({
    productId: new NonNullableFormControl(''),
    sizeId: new NonNullableFormControl(''),
  });

  /** Rankings de Vendas Reais (Pedidos Concluídos) */
  readonly salesByQuantity = computed(() => {
    return [...this.realSalesProducts()].sort((a, b) => b.quantitySold - a.quantitySold);
  });

  readonly displayedSalesByQuantity = computed(() => {
    const limit = this.salesQtyLimit();
    const items = this.salesByQuantity();
    return limit > 0 ? items.slice(0, limit) : items;
  });

  readonly maxQuantitySold = computed(() => {
    const items = this.salesByQuantity();
    if (!items || items.length === 0) return 1;
    const max = items[0].quantitySold;
    return max > 0 ? max : 1;
  });

  readonly salesByProfit = computed(() => {
    return [...this.realSalesProducts()].sort((a, b) => b.grossProfit - a.grossProfit);
  });

  readonly displayedSalesByProfit = computed(() => {
    const limit = this.salesProfitLimit();
    const items = this.salesByProfit();
    return limit > 0 ? items.slice(0, limit) : items;
  });

  readonly maxRealGrossProfit = computed(() => {
    const items = this.salesByProfit();
    if (!items || items.length === 0) return 1;
    const max = items[0].grossProfit;
    return max > 0 ? max : 1;
  });

  /** Rankings teóricos de Ficha Técnica unitária */
  readonly displayedProfitRanking = computed(() => {
    const limit = this.profitLimit();
    const items = this.profitRanking();
    return limit > 0 ? items.slice(0, limit) : items;
  });

  readonly displayedCostRanking = computed(() => {
    const limit = this.costLimit();
    const items = this.costRanking();
    return limit > 0 ? items.slice(0, limit) : items;
  });

  /** Maior valor de lucro no ranking teórico para cálculo de proporção relativa (%) */
  readonly maxProfit = computed(() => {
    const items = this.profitRanking();
    if (!items || items.length === 0) return 1;
    const max = Math.max(...items.map((i) => i.value));
    return max > 0 ? max : 1;
  });

  /** Maior valor de custo no ranking teórico para cálculo de proporção relativa (%) */
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
    const cleanName = productName.trim().toLowerCase();
    const prod = this.productMap().get(cleanName);
    if (prod?.imageUrl) return prod.imageUrl;

    // Busca aproximada caso o nome seja "Bacon" vs "Pizza Bacon" ou contenha o sabor
    for (const [key, p] of this.productMap().entries()) {
      if (key.includes(cleanName) || cleanName.includes(key)) {
        if (p.imageUrl) return p.imageUrl;
      }
    }
    return null;
  }

  formatQty(qty: number): string {
    return new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 1 }).format(qty);
  }

  getMarginStatusFromPct(marginPercentage: number): MarginStatus {
    return marginPercentage >= 20 ? 'Within' : 'NeedsAdjustment';
  }

  /** Itens filtrados para o gráfico de margens (Vendas Reais ou Ficha Técnica) */
  readonly filteredMargins = computed<ChartMarginPoint[]>(() => {
    const source = this.chartSource();
    let all: ChartMarginPoint[] = [];

    if (source === 'sales') {
      all = this.realSalesProducts().map((p) => ({
        name: p.productName,
        marginPercentage: p.marginPercentage,
        revenue: p.revenue,
        cost: p.cost,
        grossProfit: p.grossProfit,
        quantitySold: p.quantitySold,
      }));
    } else {
      all = this.margins().map((m) => ({
        name: m.name,
        marginPercentage: m.marginPercentage,
      }));
    }

    if (!all || all.length === 0) return [];

    const size = this.chartSizeFilter();
    const filteredBySize =
      source === 'sheet' && size !== 'all'
        ? all.filter((p) => p.name.toLowerCase().includes(size.toLowerCase()))
        : all;

    const mode = this.chartMode();
    if (mode === 'top10') {
      return [...filteredBySize]
        .sort((a, b) => b.marginPercentage - a.marginPercentage)
        .slice(0, 10);
    }
    if (mode === 'bottom10') {
      // Menores margens primeiro (ordem crescente para evidenciar gargalos)
      return [...filteredBySize]
        .sort((a, b) => a.marginPercentage - b.marginPercentage)
        .slice(0, 10);
    }
    if (mode === 'highlights') {
      // Destaques: Top maiores e menores margens
      if (filteredBySize.length <= 10) {
        return [...filteredBySize].sort((a, b) => b.marginPercentage - a.marginPercentage);
      }
      const sortedDesc = [...filteredBySize].sort((a, b) => b.marginPercentage - a.marginPercentage);
      const top5 = sortedDesc.slice(0, 5);
      const bottom5 = sortedDesc.slice(-5);
      const map = new Map<string, ChartMarginPoint>();
      for (const item of [...top5, ...bottom5]) {
        map.set(item.name, item);
      }
      return Array.from(map.values()).sort((a, b) => b.marginPercentage - a.marginPercentage);
    }
    // 'all'
    return [...filteredBySize].sort((a, b) => b.marginPercentage - a.marginPercentage);
  });

  /** Resumo executivo dos dados do gráfico */
  readonly chartSummary = computed(() => {
    const items = this.filteredMargins();

    if (!items || items.length === 0) {
      return {
        avg: 0,
        weightedAvg: 0,
        maxItem: null as ChartMarginPoint | null,
        minItem: null as ChartMarginPoint | null,
        healthyCount: 0,
        healthyPct: 0,
        total: 0,
        isStandardized: false,
      };
    }

    const sum = items.reduce((acc, i) => acc + i.marginPercentage, 0);
    const avg = sum / items.length;

    let totalRev = 0;
    let totalProfit = 0;
    let hasFinancials = false;

    let max = items[0];
    let min = items[0];
    let healthyCount = 0;

    for (const item of items) {
      if (item.marginPercentage > max.marginPercentage) max = item;
      if (item.marginPercentage < min.marginPercentage) min = item;
      if (item.marginPercentage >= 40) healthyCount++;
      if (item.revenue !== undefined && item.grossProfit !== undefined) {
        totalRev += item.revenue;
        totalProfit += item.grossProfit;
        hasFinancials = true;
      }
    }

    const weightedAvg = hasFinancials && totalRev > 0 ? (totalProfit / totalRev) * 100 : avg;
    const healthyPct = (healthyCount / items.length) * 100;
    const isStandardized = Math.abs(max.marginPercentage - min.marginPercentage) < 0.15;

    return {
      avg,
      weightedAvg,
      maxItem: max,
      minItem: min,
      healthyCount,
      healthyPct,
      total: items.length,
      isStandardized,
    };
  });

  /** Altura dinâmica para garantir legibilidade perfeita nas barras horizontais */
  readonly chartContainerHeight = computed(() => {
    const count = this.filteredMargins().length;
    if (count <= 3) return 220;
    if (count <= 5) return 260;
    if (count <= 10) return 380;
    return Math.min(750, Math.max(380, count * 32 + 50));
  });

  readonly chartType = 'bar' as const;
  readonly chartOptions: ChartOptions<'bar'> = {
    indexAxis: 'y', // Barras horizontais para nomes 100% legíveis sem rotação
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: { display: false },
      tooltip: {
        backgroundColor: '#0f172a',
        titleColor: '#f8fafc',
        bodyColor: '#e2e8f0',
        borderColor: 'rgba(255, 255, 255, 0.1)',
        borderWidth: 1,
        titleFont: { size: 12, weight: 'bold' },
        bodyFont: { size: 12 },
        padding: 10,
        cornerRadius: 8,
        callbacks: {
          label: (context) => {
            const rawVal = Number(context.raw);
            const points = this.filteredMargins();
            const point = points[context.dataIndex];
            const lines: string[] = [` Margem: ${rawVal.toFixed(1)}%`];
            if (point?.grossProfit !== undefined && point?.quantitySold !== undefined) {
              const profitBrl = new Intl.NumberFormat('pt-BR', {
                style: 'currency',
                currency: 'BRL',
              }).format(point.grossProfit);
              lines.push(` Lucro Total: ${profitBrl} (${point.quantitySold} un vendidas)`);
            }
            return lines;
          },
        },
      },
    },
    scales: {
      x: {
        beginAtZero: true,
        grid: {
          color: 'rgba(148, 163, 184, 0.12)',
        },
        ticks: {
          color: '#64748b',
          callback: (value: string | number) => `${value}%`,
        },
      },
      y: {
        grid: {
          display: false,
        },
        ticks: {
          color: '#334155',
          font: {
            size: 11,
            weight: 500,
          },
          autoSkip: false,
        },
      },
    },
  };

  readonly chartData = (): ChartConfiguration<'bar'>['data'] => {
    const points = this.filteredMargins();
    return {
      labels: points.map((p) => p.name),
      datasets: [
        {
          label: 'Margem %',
          data: points.map((p) => Number(p.marginPercentage.toFixed(1))),
          backgroundColor: points.map((p) =>
            p.marginPercentage >= 40
              ? 'rgba(16, 185, 129, 0.85)' // verde esmeralda para margens saudáveis (≥ 40%)
              : p.marginPercentage >= 20
              ? 'rgba(245, 158, 11, 0.85)' // âmbar para margens medianas (20-39%)
              : 'rgba(239, 68, 68, 0.85)' // vermelho para atenção (< 20%)
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

  setRankingSource(source: RankingSource): void {
    this.rankingSource.set(source);
  }

  setSalesQtyLimit(limit: number): void {
    this.salesQtyLimit.set(limit);
  }

  setSalesProfitLimit(limit: number): void {
    this.salesProfitLimit.set(limit);
  }

  setProfitLimit(limit: number): void {
    this.profitLimit.set(limit);
  }

  setCostLimit(limit: number): void {
    this.costLimit.set(limit);
  }

  setChartSource(source: ChartSource): void {
    this.chartSource.set(source);
  }

  setChartMode(mode: ChartViewMode): void {
    this.chartMode.set(mode);
  }

  setChartSizeFilter(size: string): void {
    this.chartSizeFilter.set(size);
  }

  reload(): void {
    this.loading.set(true);
    forkJoin({
      summary: this.dashboard.summary(),
      profit: this.dashboard.rankings('profit', 'desc'),
      cost: this.dashboard.rankings('cost', 'desc'),
      margins: this.dashboard.marginsChart(),
      realSales: this.profitService.byProduct('', '').pipe(catchError(() => of([] as ProfitByProductDto[]))),
    })
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: ({ summary, profit, cost, margins, realSales }) => {
          this.summary.set(summary);
          this.profitRanking.set(profit.items);
          this.costRanking.set(cost.items);
          this.margins.set(margins);
          this.realSalesProducts.set(realSales);

          // Vendas Reais deve vir selecionado por padrão tanto nos rankings quanto no gráfico quando houver vendas!
          if (realSales && realSales.length > 0) {
            this.rankingSource.set('sales');
            this.chartSource.set('sales');
          } else {
            if (profit.items.length > 0) {
              this.rankingSource.set('sheet');
            }
            if (margins.length > 0) {
              this.chartSource.set('sheet');
            }
          }
        },
        error: () => undefined,
      });
  }
}
