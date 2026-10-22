import { MarginStatus } from './common.models';
import { CostBreakdownItem } from './preparation.models';

/** POST /pricing/preview | /technicalsheets/{id}/calculate (docs/08 §4) */
export interface PricingResult {
  directCost: number;
  indirectCost: number;
  totalCost: number;
  suggestedPrice: number;
  cardPrice?: number | null;
  taxValue: number;
  estimatedProfit: number;
  marginRealPercentage: number;
  marginTarget: number;
  status: MarginStatus;
  breakdown: CostBreakdownItem[];
}

/** Resultado de pizza mista (meio a meio / quatro sabores) */
export interface PricingFlavor {
  name: string;
  cost: number;
  price: number;
}

export interface MixedPricingResult extends PricingResult {
  flavors: PricingFlavor[];
}

/** POST /pricing/half-half e /pricing/four-flavors */
export interface MixedPricingRequest {
  sizeId: string;
  productIds: string[];
}

/** Item de combo (request e resposta) */
export interface ComboItem {
  productId: string;
  sizeId?: string | null;
  quantity: number;
  /** Enriquecimento opcional vindo do GET /combos */
  productName?: string;
  sizeName?: string;
}

/** GET/POST /combos, PUT /combos/{id} */
export interface Combo {
  id: string;
  name: string;
  discountValue: number;
  fixedPrice?: number | null;
  items: ComboItem[];
}

/** POST /pricing/combos/preview e POST /combos/{id}/calculate */
export interface ComboPreviewResult {
  totalCost: number;
  totalPrice: number;
  taxValue: number;
  profit: number;
  marginPercentage: number;
  status: MarginStatus;
}

/** GET /dashboard/summary */
export interface DashboardSummary {
  totalItems: number;
  itemsInRed: number;
  monthlyFixedCost: number;
  indirectCostPerPizza: number;
  averageMargin: number;
  openAlerts: number;
}

/** GET /dashboard/rankings — item do ranking */
export interface RankingItem {
  name: string;
  size: string;
  value: number;
  marginStatus: MarginStatus;
}

/** GET /dashboard/margins-chart — ponto do gráfico de margens */
export interface MarginChartPoint {
  name: string;
  marginPercentage: number;
}
