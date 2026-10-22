import { OrderStatus, OrderType, Unit } from './common.models';

/** Linha da receita resumida exibida no card da produção (somente leitura, sem custo). */
export interface KitchenRecipeLine {
  label: string;
  quantity: number;
  unit: Unit;
}

/** Sabor do item na cozinha com fração (100%, 50%, 25%) e receita resumida. */
export interface KitchenFlavor {
  productName: string;
  imageUrl?: string | null;
  sharePercentage: number;
  recipe: KitchenRecipeLine[];
}

export type KitchenFlavorDto = KitchenFlavor;

/** Item do pedido na fila da produção. */
export interface KitchenItem {
  quantity: number;
  sizeName?: string | null;
  comboName?: string | null;
  notes?: string | null;
  flavors: KitchenFlavor[];
}

/** Card de pedido exibido no kanban da cozinha (docs/10, Sprint 3). */
export interface KitchenQueueItem {
  id: string;
  code: string;
  status: OrderStatus;
  type: OrderType;
  customerName?: string | null;
  notes?: string | null;
  placedAt: string;
  startedAt?: string | null;
  readyAt?: string | null;
  elapsedMinutes: number;
  late: boolean;
  items: KitchenItem[];
}

/** Contagem de pedidos ativos por coluna do kanban. */
export interface KitchenCounts {
  received: number;
  inPreparation: number;
  ready: number;
  total: number;
}

/** Resposta de `GET /api/v1/kitchen/queue`. */
export interface KitchenQueueResponse {
  items: KitchenQueueItem[];
  counts: KitchenCounts;
  generatedAt: string;
}
