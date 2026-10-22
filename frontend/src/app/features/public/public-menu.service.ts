import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../../environments/environment';
import {
  CartDraftItem,
  CreatePublicOrderInput,
  OrderDto,
  PublicMenuDto,
  PublicOrderTrackingDto,
} from '../../core/models/order.models';

@Injectable({ providedIn: 'root' })
export class PublicMenuService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/public`;

  private currentSlug = '';
  readonly cart = signal<CartDraftItem[]>([]);
  readonly menuCache = signal<PublicMenuDto | null>(null);

  initCartForSlug(slug: string): void {
    const normalized = (slug || '').trim().toLowerCase();
    if (this.currentSlug === normalized && this.cart().length > 0) {
      return;
    }
    this.currentSlug = normalized;
    try {
      const raw = localStorage.getItem(this.storageKey(normalized));
      if (raw) {
        const parsed = JSON.parse(raw) as CartDraftItem[];
        if (Array.isArray(parsed)) {
          this.cart.set(parsed);
          return;
        }
      }
    } catch {
      // ignora falha de storage
    }
    this.cart.set([]);
  }

  addItem(slug: string, item: Omit<CartDraftItem, 'uid'>): void {
    this.initCartForSlug(slug);
    const uid =
      typeof crypto !== 'undefined' && 'randomUUID' in crypto
        ? crypto.randomUUID()
        : `${Date.now()}-${Math.random()}`;
    const updated = [...this.cart(), { ...item, uid }];
    this.saveCart(slug, updated);
  }

  updateQuantity(slug: string, uid: string, delta: number): void {
    this.initCartForSlug(slug);
    const updated = this.cart()
      .map((it) => (it.uid === uid ? { ...it, quantity: it.quantity + delta } : it))
      .filter((it) => it.quantity > 0);
    this.saveCart(slug, updated);
  }

  clearCart(slug: string): void {
    this.saveCart(slug, []);
  }

  subtotal(): number {
    return this.cart().reduce((acc, it) => acc + it.unitPrice * it.quantity, 0);
  }

  totalCount(): number {
    return this.cart().reduce((acc, it) => acc + it.quantity, 0);
  }

  getMenu(slug: string): Observable<PublicMenuDto> {
    return this.http.get<PublicMenuDto>(`${this.base}/${encodeURIComponent(slug)}/menu`);
  }

  createOrder(
    slug: string,
    input: CreatePublicOrderInput,
    idempotencyKey?: string,
  ): Observable<OrderDto> {
    const key =
      idempotencyKey ??
      (typeof crypto !== 'undefined' && 'randomUUID' in crypto
        ? crypto.randomUUID()
        : `${Date.now()}-${Math.random()}`);
    const headers = new HttpHeaders({ 'Idempotency-Key': key });
    return this.http.post<OrderDto>(`${this.base}/${encodeURIComponent(slug)}/orders`, input, {
      headers,
    });
  }

  trackOrder(slug: string, code: string): Observable<PublicOrderTrackingDto> {
    return this.http.get<PublicOrderTrackingDto>(
      `${this.base}/${encodeURIComponent(slug)}/orders/${encodeURIComponent(code)}`,
    );
  }

  private saveCart(slug: string, items: CartDraftItem[]): void {
    const normalized = (slug || '').trim().toLowerCase();
    this.currentSlug = normalized;
    this.cart.set(items);
    try {
      localStorage.setItem(this.storageKey(normalized), JSON.stringify(items));
    } catch {
      // ignora falha de storage
    }
  }

  private storageKey(slug: string): string {
    return `pz_public_cart_${slug}`;
  }
}
