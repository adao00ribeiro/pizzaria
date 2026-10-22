import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../../environments/environment';
import { OrderStatus, OrderType, PagedResult } from '../../core/models/common.models';
import {
  CartDraftItem,
  CreateOrderInput,
  OrderDto,
  OrderListItemDto,
  OrderSummaryDto,
  OrderTicketDto,
  PublicMenuDto,
} from '../../core/models/order.models';

export interface OrderFilterParams {
  status?: OrderStatus | '';
  type?: OrderType | '';
  search?: string;
  from?: string;
  to?: string;
  page?: number;
  pageSize?: number;
}

@Injectable({ providedIn: 'root' })
export class OrdersService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/orders`;

  /**
   * Carrinho em memória do modal "Novo pedido" (Sprint 4):
   * fechar o modal acidentalmente não apaga os itens já adicionados.
   */
  readonly draftCart = signal<CartDraftItem[]>([]);

  readonly draftSubtotal = computed(() =>
    this.draftCart().reduce((acc, item) => acc + item.unitPrice * item.quantity, 0),
  );

  addDraftItem(item: Omit<CartDraftItem, 'uid'>): void {
    const uid =
      typeof crypto !== 'undefined' && 'randomUUID' in crypto
        ? crypto.randomUUID()
        : `${Date.now()}-${Math.random()}`;
    this.draftCart.update((items) => [...items, { ...item, uid }]);
  }

  updateDraftQuantity(uid: string, delta: number): void {
    this.draftCart.update((items) =>
      items
        .map((it) => (it.uid === uid ? { ...it, quantity: it.quantity + delta } : it))
        .filter((it) => it.quantity > 0),
    );
  }

  removeDraftItem(uid: string): void {
    this.draftCart.update((items) => items.filter((it) => it.uid !== uid));
  }

  clearDraft(): void {
    this.draftCart.set([]);
  }

  list(filter: OrderFilterParams = {}): Observable<PagedResult<OrderListItemDto>> {
    let params = new HttpParams();
    if (filter.status) params = params.set('status', filter.status);
    if (filter.type) params = params.set('type', filter.type);
    if (filter.search?.trim()) params = params.set('search', filter.search.trim());
    if (filter.from) params = params.set('from', filter.from);
    if (filter.to) params = params.set('to', filter.to);
    if (filter.page) params = params.set('page', String(filter.page));
    if (filter.pageSize) params = params.set('pageSize', String(filter.pageSize));
    return this.http.get<PagedResult<OrderListItemDto>>(this.base, { params });
  }

  todaySummary(): Observable<OrderSummaryDto> {
    return this.http.get<OrderSummaryDto>(`${this.base}/today`);
  }

  catalog(): Observable<PublicMenuDto> {
    return this.http.get<PublicMenuDto>(`${this.base}/catalog`);
  }

  getById(id: string): Observable<OrderDto> {
    return this.http.get<OrderDto>(`${this.base}/${id}`);
  }

  getTicket(id: string): Observable<OrderTicketDto> {
    return this.http.get<OrderTicketDto>(`${this.base}/${id}/ticket`);
  }

  create(input: CreateOrderInput, idempotencyKey?: string): Observable<OrderDto> {
    const key =
      idempotencyKey ??
      (typeof crypto !== 'undefined' && 'randomUUID' in crypto
        ? crypto.randomUUID()
        : `${Date.now()}-${Math.random()}`);
    const headers = new HttpHeaders({ 'Idempotency-Key': key });
    return this.http.post<OrderDto>(this.base, input, { headers });
  }

  changeStatus(id: string, status: OrderStatus, note?: string): Observable<OrderDto> {
    return this.http.post<OrderDto>(`${this.base}/${id}/status`, { status, note });
  }

  cancel(id: string, reason: string): Observable<OrderDto> {
    return this.http.post<OrderDto>(`${this.base}/${id}/cancel`, { reason });
  }
}
