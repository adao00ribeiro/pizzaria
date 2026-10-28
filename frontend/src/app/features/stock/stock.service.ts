import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';

import { environment } from '../../../environments/environment';
import { PAGE_SIZE, PagedResult } from '../../core/models/common.models';
import {
  StockBalance,
  StockMovement,
  StockMovementRequest,
  StockReplenishmentFilter,
  StockReplenishmentReport,
} from '../../core/models/stock.models';
import { normalizePage } from '../../core/utils/page.util';

/** Saldos e movimentos de estoque (docs/07 §12). */
@Injectable({ providedIn: 'root' })
export class StockService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/stock`;

  balances(params: { belowMinimum?: boolean; page?: number }): Observable<PagedResult<StockBalance>> {
    let httpParams = new HttpParams().set('page', params.page ?? 1).set('pageSize', PAGE_SIZE);
    if (params.belowMinimum) {
      httpParams = httpParams.set('belowMinimum', 'true');
    }
    return this.http
      .get<PagedResult<StockBalance> | StockBalance[]>(`${this.base}/balances`, { params: httpParams })
      .pipe(map(normalizePage));
  }

  movements(params: { ingredientId?: string; page?: number }): Observable<PagedResult<StockMovement>> {
    let httpParams = new HttpParams().set('page', params.page ?? 1).set('pageSize', PAGE_SIZE);
    if (params.ingredientId) {
      httpParams = httpParams.set('ingredientId', params.ingredientId);
    }
    return this.http
      .get<PagedResult<StockMovement> | StockMovement[]>(`${this.base}/movements`, { params: httpParams })
      .pipe(map(normalizePage));
  }

  createMovement(body: StockMovementRequest): Observable<unknown> {
    return this.http.post(`${this.base}/movements`, body);
  }

  getReplenishmentReport(filter?: StockReplenishmentFilter): Observable<StockReplenishmentReport> {
    let httpParams = new HttpParams();
    if (filter?.urgency) {
      httpParams = httpParams.set('urgency', filter.urgency);
    }
    if (filter?.onlyNeedingReplenishment !== undefined) {
      httpParams = httpParams.set('onlyNeedingReplenishment', String(filter.onlyNeedingReplenishment));
    }
    if (filter?.search?.trim()) {
      httpParams = httpParams.set('search', filter.search.trim());
    }
    return this.http.get<StockReplenishmentReport>(`${this.base}/replenishment-report`, { params: httpParams });
  }
}
