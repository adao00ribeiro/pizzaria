/**
 * Modelos de catálogo de bairros e regiões (Sprint IA-2 e IA-4).
 * Espelham NeighborhoodDto e NeighborhoodInputDto do backend.
 */

export type NeighborhoodRegion = 'Center' | 'North' | 'South' | 'East' | 'West' | 'Other';

export const NEIGHBORHOOD_REGION_LABELS: Record<NeighborhoodRegion, string> = {
  Center: 'Centro',
  North: 'Zona Norte',
  South: 'Zona Sul',
  East: 'Zona Leste',
  West: 'Zona Oeste',
  Other: 'Outra / Não mapeada',
};

export const NEIGHBORHOOD_REGIONS: readonly NeighborhoodRegion[] = [
  'Center',
  'North',
  'South',
  'East',
  'West',
  'Other',
];

export interface NeighborhoodDto {
  id: string;
  name: string;
  region: NeighborhoodRegion;
  isActive: boolean;
}

export interface NeighborhoodInputDto {
  name: string;
  region: NeighborhoodRegion;
  isActive?: boolean;
}
