export interface ProfitReportDto {
  from: string;
  to: string;
  revenue: number;
  ingredientCost: number;
  stockOutCost: number;
  hasCostDivergence: boolean;
  costDivergenceAmount: number;
  estimatedCardFee: number;
  grossProfit: number;
  grossMarginPercentage: number;
  completedOrdersCount: number;
  cancelledOrdersCount: number;
  averageTicket: number;
  monthlyFixedCostTotal: number;
  proportionalFixedCost: number;
  netResultAfterFixedCosts: number;
}

export interface ProfitByDayDto {
  date: string;
  ordersCount: number;
  revenue: number;
  cost: number;
  grossProfit: number;
  marginPercentage: number;
}

export interface ProfitByProductDto {
  productId: string;
  productName: string;
  quantitySold: number;
  revenue: number;
  cost: number;
  grossProfit: number;
  marginPercentage: number;
}

export interface ProfitByChannelDto {
  channel: string;
  channelLabel: string;
  ordersCount: number;
  revenue: number;
  cost: number;
  grossProfit: number;
  marginPercentage: number;
  revenueSharePercentage: number;
}
