import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';

import { environment } from '../../../environments/environment';
import { FixedCost, FixedCostRequest } from '../../core/models/cost.models';
import { PAGE_SIZE, PagedResult } from '../../core/models/common.models';
import { normalizePage } from '../../core/utils/page.util';

/** CRUD de custos fixos (docs/07 §4). */
@Injectable({ providedIn: 'root' })
export class FixedCostsService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/fixedcosts`;

  list(page = 1): Observable<PagedResult<FixedCost>> {
    const params = new HttpParams().set('page', page).set('pageSize', PAGE_SIZE);
    return this.http
      .get<PagedResult<FixedCost> | FixedCost[]>(this.base, { params })
      .pipe(map(normalizePage));
  }

  create(body: FixedCostRequest): Observable<FixedCost> {
    return this.http.post<FixedCost>(this.base, body);
  }

  update(id: string, body: FixedCostRequest): Observable<FixedCost> {
    return this.http.put<FixedCost>(`${this.base}/${id}`, body);
  }

  remove(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }
}
