import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';

import { environment } from '../../../environments/environment';
import { PAGE_SIZE, PagedResult } from '../../core/models/common.models';
import {
  DashboardSummary,
  MarginChartPoint,
  RankingItem,
} from '../../core/models/pricing.models';
import { SheetDetail } from '../../core/models/sheet.models';
import { normalizeItems, normalizePage } from '../../core/utils/page.util';

/** Indicadores, rankings e consulta individual do dashboard (docs/07 §10). */
@Injectable({ providedIn: 'root' })
export class DashboardService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/dashboard`;

  summary(): Observable<DashboardSummary> {
    return this.http.get<DashboardSummary>(`${this.base}/summary`);
  }

  rankings(
    by: 'profit' | 'cost',
    direction: 'desc' | 'asc' = 'desc',
    page = 1,
  ): Observable<PagedResult<RankingItem>> {
    const params = { by, direction, page, pageSize: PAGE_SIZE };
    return this.http
      .get<PagedResult<RankingItem> | RankingItem[]>(`${this.base}/rankings`, { params })
      .pipe(map(normalizePage));
  }

  marginsChart(): Observable<MarginChartPoint[]> {
    return this.http
      .get<PagedResult<MarginChartPoint> | MarginChartPoint[]>(`${this.base}/margins-chart`)
      .pipe(map(normalizeItems));
  }

  /** Composição detalhada do cálculo de um produto × tamanho. */
  individual(productId: string, sizeId: string): Observable<SheetDetail> {
    return this.http.get<SheetDetail>(`${this.base}/individual`, {
      params: { productId, sizeId },
    });
  }
}
