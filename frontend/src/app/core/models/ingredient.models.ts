import { Unit } from './common.models';

/** GET /ingredients — item da listagem (com custo calculado pelo servidor) */
export interface Ingredient {
  id: string;
  name: string;
  purchaseUnit: Unit;
  purchaseQuantity: number;
  purchasePrice: number;
  lossPercentage: number;
  /** Custo por unidade de compra, calculado pela API */
  unitCost: number;
  minimumStock: number;
  currentStock: number;
}

/** Body POST/PUT /ingredients (docs/07 §2) */
export interface IngredientRequest {
  name: string;
  purchaseUnit: Unit;
  purchaseQuantity: number;
  purchasePrice: number;
  lossPercentage: number;
  minimumStock: number;
}

export interface UsageReference {
  id: string;
  name: string;
}

export interface SheetUsageReference {
  id: string;
  productName: string;
  sizeName: string;
}

/** GET /ingredients/{id}/usages */
export interface IngredientUsages {
  preparations: UsageReference[];
  sheets: SheetUsageReference[];
}
