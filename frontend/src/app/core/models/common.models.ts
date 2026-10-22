/**
 * Tipos compartilhados: paginação, erro padronizado e enums da API.
 * Os enums chegam como strings (contrato docs/07).
 */

/** Envelopamento de listagem: `{ "items": [...], "total": 123 }` */
export interface PagedResult<T> {
  items: T[];
  total: number;
}

export interface ApiFieldError {
  field: string;
  detail: string;
}

/** Resposta de erro padronizada da API */
export interface ApiError {
  status: number;
  message: string;
  errors?: ApiFieldError[];
}

/** Unidade de compra/uso de ingredientes e preparos */
export type Unit = 'g' | 'ml' | 'un' | 'kg' | 'L' | 'porcao';

/** Lista completa das unidades, para uso em selects */
export const UNITS: readonly Unit[] = ['g', 'ml', 'un', 'kg', 'L', 'porcao'];

/** Categoria do produto */
export type ProductCategory = 'Pizza' | 'Beverage' | 'Other';

/** Status de margem de um item precificado */
export type MarginStatus = 'Within' | 'NeedsAdjustment';

/** Tipo do alerta */
export type AlertType = 'MarginBelow' | 'LowStock';

/** Status do alerta */
export type AlertStatus = 'Open' | 'Resolved';

/** Tipo de movimento de estoque */
export type StockMovementType = 'In' | 'Out' | 'Adjustment' | 'OrderOut';

/** Status do pedido (docs/10, Sprints 2–5) */
export type OrderStatus =
  | 'Received'
  | 'InPreparation'
  | 'Ready'
  | 'OutForDelivery'
  | 'Completed'
  | 'Cancelled';

/** Tipo de atendimento do pedido */
export type OrderType = 'Counter' | 'Pickup' | 'Delivery';

/** Forma de pagamento informativa (P8) */
export type PaymentMethod = 'Cash' | 'Card' | 'Pix';

/** Rateio do custo fixo */
export type AllocationType = 'PerPizza' | 'RevenuePercentage';

/** Tipo de item de receita/ficha técnica */
export type SheetItemType = 'Ingredient' | 'Preparation';

export const PAGE_SIZE = 20;
