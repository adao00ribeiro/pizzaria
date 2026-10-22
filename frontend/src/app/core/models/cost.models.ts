import { AllocationType } from './common.models';

/** GET/POST /fixedcosts — custo fixo mensal */
export interface FixedCost {
  id: string;
  description: string;
  monthlyValue: number;
  allocationType: AllocationType;
  expectedPizzasPerMonth?: number | null;
  expectedMonthlyRevenue?: number | null;
}

/** Body POST/PUT /fixedcosts */
export interface FixedCostRequest {
  description: string;
  monthlyValue: number;
  allocationType: AllocationType;
  expectedPizzasPerMonth?: number | null;
  expectedMonthlyRevenue?: number | null;
}

/** GET/PUT /fees — configuração única de taxas do tenant (docs/07 §4) */
export interface Fees {
  cardFeePercentage: number;
  deliveryFeePercentage: number;
  deliveryFixedValue: number;
  defaultMarginPercentage: number;
  lossPercentageGlobal: number;
}
