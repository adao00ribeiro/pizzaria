import {
  AlertStatus,
  AlertType,
  AllocationType,
  MarginStatus,
  OrderStatus,
  OrderType,
  PaymentMethod,
  ProductCategory,
  StockMovementType,
  Unit,
} from './common.models';

/** Rótulos em português para os valores que chegam como enum string da API. */

export const UNIT_LABELS: Record<Unit, string> = {
  g: 'g (gramas)',
  ml: 'ml (mililitros)',
  un: 'un (unidades)',
  kg: 'kg (quilos)',
  L: 'L (litros)',
  porcao: 'porção',
};

export const CATEGORY_LABELS: Record<ProductCategory, string> = {
  Pizza: 'Pizza',
  Beverage: 'Bebida',
  Other: 'Outro',
};

export const MARGIN_STATUS_LABELS: Record<MarginStatus, string> = {
  Within: 'Dentro da margem',
  NeedsAdjustment: 'Precisa ajustar',
};

export const ALERT_TYPE_LABELS: Record<AlertType, string> = {
  MarginBelow: 'Margem abaixo do alvo',
  LowStock: 'Estoque abaixo do mínimo',
};

export const ALERT_STATUS_LABELS: Record<AlertStatus, string> = {
  Open: 'Aberto',
  Resolved: 'Resolvido',
};

export const STOCK_TYPE_LABELS: Record<StockMovementType, string> = {
  In: 'Entrada',
  Out: 'Saída',
  Adjustment: 'Ajuste',
  OrderOut: 'Baixa de pedido',
};

export const ORDER_STATUS_LABELS: Record<OrderStatus, string> = {
  Received: 'Recebido',
  InPreparation: 'Em preparo',
  Ready: 'Pronto',
  OutForDelivery: 'Saiu para entrega',
  Completed: 'Concluído',
  Cancelled: 'Cancelado',
};

export const ORDER_TYPE_LABELS: Record<OrderType, string> = {
  Counter: 'Balcão',
  Pickup: 'Retirada',
  Delivery: 'Entrega',
};

export const PAYMENT_METHOD_LABELS: Record<PaymentMethod, string> = {
  Cash: 'Dinheiro',
  Card: 'Cartão',
  Pix: 'Pix',
};

export const ALLOCATION_LABELS: Record<AllocationType, string> = {
  PerPizza: 'Por pizza',
  RevenuePercentage: '% do faturamento',
};

export const ITEM_TYPE_LABELS: Record<'Ingredient' | 'Preparation', string> = {
  Ingredient: 'Ingrediente',
  Preparation: 'Preparo',
};
