import { ProductCategory } from './common.models';

export interface Product {
  id: string;
  name: string;
  category: ProductCategory;
  isActive: boolean;
  /** URL da imagem do produto (opcional) */
  imageUrl?: string | null;
  /** Presente para bebidas (precificação simples) */
  purchasePrice?: number | null;
  markupMultiplier?: number | null;
  suggestedPrice?: number | null;
  ingredientId?: string | null;
  currentStock?: number | null;
  minimumStock?: number | null;
}

/** Body POST/PUT /products */
export interface ProductRequest {
  name: string;
  category: ProductCategory;
  isActive: boolean;
  imageUrl?: string | null;
  purchasePrice?: number | null;
  markupMultiplier?: number | null;
  currentStock?: number | null;
  minimumStock?: number | null;
}

export type ProductDto = Product;
export type ProductInput = ProductRequest;

/** GET/POST /sizes — tamanhos de pizza */
export interface Size {
  id: string;
  name: string;
  diameterCm: number | null;
}

/** Body POST/PUT /sizes */
export interface SizeRequest {
  name: string;
  diameterCm: number | null;
}

/** GET /beverages — produto categoria Beverage com precificação calculada */
export interface Beverage {
  id: string;
  name: string;
  purchasePrice: number;
  markupMultiplier: number;
  suggestedPrice: number;
  marginPercentage: number;
  imageUrl?: string | null;
  currentStock: number;
  minimumStock: number;
  belowMinimumStock: boolean;
}

/** PUT /beverages/{id}/markup */
export interface MarkupRequest {
  markupMultiplier: number;
  purchasePrice?: number;
  currentStock?: number;
  minimumStock?: number;
}
