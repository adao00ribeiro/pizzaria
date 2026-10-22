import { MarginStatus, SheetItemType, Unit } from './common.models';
import { PricingResult } from './pricing.models';

/** GET /technicalsheets — item da listagem de fichas precificadas */
export interface SheetListItem {
  id: string;
  productName: string;
  sizeName: string;
  directCost: number;
  indirectCost: number;
  suggestedPrice: number;
  estimatedProfit: number;
  marginRealPercentage: number;
  marginStatus: MarginStatus;
}

/** Item de uma ficha técnica (ingrediente ou preparo) */
export interface SheetItem {
  id: string;
  type: SheetItemType;
  ingredientId?: string | null;
  preparationId?: string | null;
  description: string;
  quantity: number;
  unit: Unit;
  unitCostSnapshot: number;
  itemCost: number;
}

/**
 * GET /technicalsheets/{id} e /dashboard/individual — detalhe com itens e
 * resultado completo aninhado (mesmo shape de POST /pricing/preview).
 */
export interface SheetDetail {
  id: string;
  productId: string;
  productName: string;
  sizeId: string;
  sizeName: string;
  packagingCost: number;
  lossPercentage: number | null;
  marginPercentage: number | null;
  result: PricingResult;
  items: SheetItem[];
}

/** Item do request de salvamento de ficha */
export interface SheetItemRequest {
  type: SheetItemType;
  ingredientId?: string;
  preparationId?: string;
  quantity: number;
  unit: Unit;
}

/** Body POST/PUT /technicalsheets */
export interface SheetSaveRequest {
  productId: string;
  sizeId: string;
  packagingCost: number;
  lossPercentage: number | null;
  marginPercentage: number;
  items: SheetItemRequest[];
}

/**
 * Body POST /pricing/preview — mesmo do salvamento, mas productId/sizeId
 * não são obrigatórios (cálculo sem salvar).
 */
export interface PricingPreviewRequest {
  productId?: string | null;
  sizeId?: string | null;
  packagingCost: number;
  lossPercentage: number | null;
  marginPercentage: number;
  items: SheetItemRequest[];
}
