import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';

import { environment } from '../../../environments/environment';
import { ProductCategory } from '../../core/models/common.models';
import {
  Product,
  ProductRequest,
  Size,
  SizeRequest,
} from '../../core/models/product.models';
import { PagedResult } from '../../core/models/common.models';
import { normalizeItems } from '../../core/utils/page.util';

/** Produtos (sabores) e tamanhos (docs/07 §5). */
@Injectable({ providedIn: 'root' })
export class ProductsService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}`;

  products(category?: ProductCategory | ''): Observable<Product[]> {
    let params = new HttpParams();
    if (category) {
      params = params.set('category', category);
    }
    return this.http
      .get<PagedResult<Product> | Product[]>(`${this.base}/products`, { params })
      .pipe(map(normalizeItems));
  }

  sizes(): Observable<Size[]> {
    return this.http
      .get<PagedResult<Size> | Size[]>(`${this.base}/sizes`)
      .pipe(map(normalizeItems));
  }

  createProduct(body: ProductRequest): Observable<Product> {
    return this.http.post<Product>(`${this.base}/products`, body);
  }

  updateProduct(id: string, body: ProductRequest): Observable<Product> {
    return this.http.put<Product>(`${this.base}/products/${id}`, body);
  }

  removeProduct(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/products/${id}`);
  }

  createSize(body: SizeRequest): Observable<Size> {
    return this.http.post<Size>(`${this.base}/sizes`, body);
  }

  updateSize(id: string, body: SizeRequest): Observable<Size> {
    return this.http.put<Size>(`${this.base}/sizes/${id}`, body);
  }

  removeSize(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/sizes/${id}`);
  }
}
