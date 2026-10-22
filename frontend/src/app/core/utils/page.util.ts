import { PagedResult } from '../models/common.models';

/**
 * Algumas listagens podem voltar como array puro (sem paginação) e outras
 * como `{ items, total }`. Normaliza para sempre devolver PagedResult.
 */
export function normalizePage<T>(res: PagedResult<T> | T[]): PagedResult<T> {
  if (Array.isArray(res)) {
    return { items: res, total: res.length };
  }
  if (res && Array.isArray(res.items)) {
    return { items: res.items, total: typeof res.total === 'number' ? res.total : res.items.length };
  }
  return { items: [], total: 0 };
}

/** Extrai apenas os itens de uma resposta que pode ser array ou envelopada. */
export function normalizeItems<T>(res: PagedResult<T> | T[]): T[] {
  return normalizePage(res).items;
}
