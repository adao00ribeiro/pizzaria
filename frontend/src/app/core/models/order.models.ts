import { OrderStatus, OrderType, PaymentMethod } from './common.models';

export interface OrderItemFlavorDto {
  productId: string;
  productName: string;
  sharePercentage: number;
}

export interface OrderItemDto {
  id: string;
  sizeId: string | null;
  sizeName: string | null;
  comboId: string | null;
  comboName: string | null;
  quantity: number;
  unitPrice: number;
  total: number;
  notes: string | null;
  flavors: OrderItemFlavorDto[];
}

export interface OrderStatusHistoryDto {
  id: string;
  fromStatus: OrderStatus | null;
  toStatus: OrderStatus;
  changedByUserId: string | null;
  note: string | null;
  changedAt: string;
}

export interface OrderDto {
  id: string;
  code: string;
  type: OrderType;
  status: OrderStatus;
  paymentMethod: PaymentMethod | null;
  customerName: string | null;
  customerPhone: string | null;
  addressDistrict?: string | null;
  addressStreet?: string | null;
  addressNumber?: string | null;
  addressComplement?: string | null;
  deliveryAddress: string | null;
  notes: string | null;
  cancellationReason: string | null;
  subtotal: number;
  deliveryFee: number;
  discount: number;
  total: number;
  placedAt: string;
  completedAt: string | null;
  cancelledAt: string | null;
  placedByUserId: string | null;
  items: OrderItemDto[];
  history: OrderStatusHistoryDto[];
}

export interface OrderListItemDto {
  id: string;
  code: string;
  type: OrderType;
  status: OrderStatus;
  paymentMethod: PaymentMethod | null;
  customerName: string | null;
  customerPhone: string | null;
  total: number;
  itemsCount: number;
  placedAt: string;
}

export interface OrderSummaryDto {
  totalOrders: number;
  totalRevenue?: number;
  received: number;
  inPreparation: number;
  ready: number;
  outForDelivery: number;
  completed: number;
  cancelled: number;
  counter?: number;
  pickup?: number;
  delivery?: number;
  receivedCount?: number;
  inPreparationCount?: number;
  readyCount?: number;
  outForDeliveryCount?: number;
  completedCount?: number;
  cancelledCount?: number;
}

export interface CreateOrderFlavorInput {
  productId: string;
  sharePercentage: number;
}

export interface CreateOrderItemInput {
  sizeId?: string | null;
  comboId?: string | null;
  quantity: number;
  notes?: string | null;
  flavors: CreateOrderFlavorInput[];
}

export interface CreateOrderInput {
  type: OrderType;
  paymentMethod?: PaymentMethod | null;
  customerName?: string | null;
  customerPhone?: string | null;
  addressDistrict?: string | null;
  addressStreet?: string | null;
  addressNumber?: string | null;
  addressComplement?: string | null;
  deliveryAddress?: string | null;
  notes?: string | null;
  discount?: number;
  items: CreateOrderItemInput[];
}

export interface CreatePublicOrderInput {
  type: OrderType;
  paymentMethod?: PaymentMethod | null;
  customerName: string;
  customerPhone: string;
  addressDistrict?: string | null;
  addressStreet?: string | null;
  addressNumber?: string | null;
  addressComplement?: string | null;
  deliveryAddress?: string | null;
  notes?: string | null;
  items: CreateOrderItemInput[];
}

export interface OrderTicketItemDto {
  quantity: number;
  description: string;
  flavorsLabel: string | null;
  notes: string | null;
  unitPrice: number;
  total: number;
}

export interface OrderTicketDto {
  orderId: string;
  pizzeriaName: string;
  code: string;
  type: OrderType;
  status: OrderStatus;
  paymentMethod: PaymentMethod | null;
  customerName: string | null;
  customerPhone: string | null;
  deliveryAddress: string | null;
  notes: string | null;
  subtotal: number;
  deliveryFee: number;
  discount: number;
  total: number;
  placedAt: string;
  disclaimer: string;
  items: OrderTicketItemDto[];
}

// ---------------- Cardápio público / Catálogo operacional (Sprints 4 e 5) ----------------

export interface PublicSizeDto {
  id: string;
  name: string;
  diameterCm: number;
}

export interface PublicPizzaPriceDto {
  sizeId: string;
  sizeName: string;
  price: number;
}

export interface PublicPizzaDto {
  productId: string;
  name: string;
  imageUrl?: string | null;
  pricesBySize: PublicPizzaPriceDto[];
}

export interface PublicComboItemDto {
  productId: string;
  productName: string;
  sizeId: string | null;
  sizeName: string | null;
  quantity: number;
}

export interface PublicComboDto {
  comboId: string;
  name: string;
  price: number;
  items: PublicComboItemDto[];
}

export interface PublicBeverageDto {
  productId: string;
  name: string;
  imageUrl?: string | null;
  price: number;
}

export interface PublicMenuDto {
  pizzeriaName: string;
  slug: string;
  deliveryFixedFee: number;
  deliveryFeePercentage: number;
  sizes: PublicSizeDto[];
  pizzas: PublicPizzaDto[];
  combos: PublicComboDto[];
  beverages: PublicBeverageDto[];
}

export interface PublicTrackingStepDto {
  status: OrderStatus;
  changedAt: string;
}

export interface PublicOrderTrackingFlavorDto {
  productName: string;
  sharePercentage: number;
}

export interface PublicOrderTrackingItemDto {
  quantity: number;
  sizeName: string | null;
  comboName: string | null;
  flavors: PublicOrderTrackingFlavorDto[];
  description?: string | null;
  unitPrice?: number;
  total?: number;
  notes?: string | null;
  flavorsLabel?: string | null;
}

export interface PublicOrderTrackingDto {
  pizzeriaName: string;
  slug?: string;
  code: string;
  type: OrderType;
  status: OrderStatus;
  paymentMethod: PaymentMethod | null;
  customerFirstName: string | null;
  cancellationReason?: string | null;
  cancelReason?: string | null;
  subtotal: number;
  deliveryFee: number;
  discount: number;
  total: number;
  placedAt: string;
  startedAt?: string | null;
  readyAt?: string | null;
  deliveredAt?: string | null;
  completedAt?: string | null;
  cancelledAt?: string | null;
  items: PublicOrderTrackingItemDto[];
  timeline?: PublicTrackingStepDto[];
}

/** Item no carrinho (atendente ou público) */
export interface CartDraftItem {
  uid: string;
  kind: 'pizza' | 'combo' | 'beverage';
  label: string;
  sublabel: string | null;
  sizeId: string | null;
  comboId: string | null;
  flavors: CreateOrderFlavorInput[];
  unitPrice: number;
  quantity: number;
  notes: string;
}
