/** GET/PUT /tenant — dados da pizzaria (tenant) */
export interface TenantInfo {
  name: string;
  /** Slug público usado no cardápio/checkout (docs/10, Sprint 1). */
  slug?: string | null;
}
