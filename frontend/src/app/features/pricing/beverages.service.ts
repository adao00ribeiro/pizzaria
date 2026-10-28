import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';

import { environment } from '../../../environments/environment';
import { Beverage, MarkupRequest } from '../../core/models/product.models';
import { PagedResult } from '../../core/models/common.models';
import { normalizeItems } from '../../core/utils/page.util';

/** Precificação de bebidas (GET /beverages, PUT /beverages/{id}/markup). */
@Injectable({ providedIn: 'root' })
export class BeveragesService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/beverages`;

  list(): Observable<Beverage[]> {
    return this.http
      .get<PagedResult<Beverage> | Beverage[]>(this.base)
      .pipe(map(normalizeItems));
  }

  updateMarkup(
    id: string,
    markupMultiplier: number,
    purchasePrice?: number,
    currentStock?: number,
    minimumStock?: number,
  ): Observable<Beverage> {
    const body: MarkupRequest = { markupMultiplier, purchasePrice, currentStock, minimumStock };
    return this.http.put<Beverage>(`${this.base}/${id}/markup`, body);
  }
}
