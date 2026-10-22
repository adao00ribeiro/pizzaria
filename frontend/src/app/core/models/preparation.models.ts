import { SheetItemType, Unit } from './common.models';

/** GET /preparations — item da listagem */
export interface Preparation {
  id: string;
  name: string;
  yieldQuantity: number;
  yieldUnit: Unit;
  totalCost: number;
  unitCost: number;
  /**
   * Composição da receita. A listagem devolve apenas o detalhe via
   * GET /preparations/{id}; o editor carrega os itens ao abrir.
   */
  items?: PreparationItem[];
  /** Presente na listagem (GET /preparations) */
  itemCount?: number;
}

/** Item da composição retornado pelo detalhe do preparo */
export interface PreparationItem {
  id: string;
  type: SheetItemType;
  ingredientId?: string | null;
  preparationId?: string | null;
  description?: string | null;
  quantity: number;
  unit: Unit;
}

/** Item de receita (ingrediente ou preparo aninhado) */
export interface PreparationItemRequest {
  type: SheetItemType;
  ingredientId?: string;
  preparationId?: string;
  quantity: number;
  unit: Unit;
}

/** Body POST/PUT /preparations (docs/07 §3) */
export interface PreparationRequest {
  name: string;
  yieldQuantity: number;
  yieldUnit: Unit;
  items: PreparationItemRequest[];
}

export interface CostBreakdownItem {
  label: string;
  cost: number;
}

/**
 * POST /preparations/calculate-preview — cálculo ao vivo do montador
 * (endpoint acordado com o backend além de docs/07).
 */
export interface PreparationCalculation {
  totalCost: number;
  unitCost: number;
  breakdown: CostBreakdownItem[];
}
