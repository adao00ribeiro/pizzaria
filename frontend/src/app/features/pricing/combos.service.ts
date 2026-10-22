import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';

import { environment } from '../../../environments/environment';
import { Combo, ComboItem, ComboPreviewResult } from '../../core/models/pricing.models';
import { PagedResult } from '../../core/models/common.models';
import { normalizeItems } from '../../core/utils/page.util';

/** Body de criação/edição de combo (POST/PUT /combos). */
export interface ComboRequest {
  name: string;
  discountValue: number;
  fixedPrice: number | null;
  items: ComboItem[];
}

/** CRUD de combos (docs/07 §8). */
@Injectable({ providedIn: 'root' })
export class CombosService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/combos`;

  list(): Observable<Combo[]> {
    return this.http
      .get<PagedResult<Combo> | Combo[]>(this.base)
      .pipe(map(normalizeItems));
  }

  create(body: ComboRequest): Observable<Combo> {
    return this.http.post<Combo>(this.base, body);
  }

  update(id: string, body: ComboRequest): Observable<Combo> {
    return this.http.put<Combo>(`${this.base}/${id}`, body);
  }

  remove(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }

  /** Recalcula o combo no servidor (recálculo em cadeia). */
  calculate(id: string): Observable<ComboPreviewResult> {
    return this.http.post<ComboPreviewResult>(`${this.base}/${id}/calculate`, {});
  }
}
