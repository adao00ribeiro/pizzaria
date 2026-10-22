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
