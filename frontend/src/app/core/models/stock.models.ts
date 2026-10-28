import { StockMovementType, Unit } from './common.models';

/** GET /stock/balances — item da listagem de saldos */
export interface StockBalance {
  ingredientId: string;
  name: string;
  currentStock: number;
  minimumStock: number;
  belowMinimum: boolean;
  unit: Unit;
}

/** GET /stock/movements — item do histórico */
export interface StockMovement {
  id: string;
  ingredientName: string;
  type: StockMovementType;
  quantity: number;
  unitCost?: number | null;
  note?: string | null;
  createdAt: string;
}

/** Body POST /stock/movements */
export interface StockMovementRequest {
  ingredientId: string;
  type: StockMovementType;
  quantity: number;
  unitCost?: number | null;
  note?: string | null;
}

export type StockReplenishmentUrgency = 'Critical' | 'Low' | 'Warning' | 'Normal';

export interface StockReplenishmentItem {
  ingredientId: string;
  ingredientName: string;
  unit: Unit;
  currentStock: number;
  minimumStock: number;
  deficitQuantity: number;
  suggestedPackagesToBuy: number;
  suggestedReplenishmentQuantity: number;
  purchaseQuantity: number;
  purchasePrice: number;
  unitCost: number;
  estimatedCost: number;
  urgency: StockReplenishmentUrgency;
}

export interface StockReplenishmentReport {
  items: StockReplenishmentItem[];
  totalItemsNeedingReplenishment: number;
  criticalCount: number;
  lowCount: number;
  warningCount: number;
  totalEstimatedCost: number;
  generatedAt: string;
}

export interface StockReplenishmentFilter {
  urgency?: StockReplenishmentUrgency | '';
  onlyNeedingReplenishment?: boolean;
  search?: string;
}
