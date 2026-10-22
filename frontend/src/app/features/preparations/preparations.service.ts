import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';

import { environment } from '../../../environments/environment';
import { PAGE_SIZE, PagedResult } from '../../core/models/common.models';
import {
  Preparation,
  PreparationCalculation,
  PreparationRequest,
} from '../../core/models/preparation.models';
import { normalizePage } from '../../core/utils/page.util';

/** CRUD de preparos + cálculo ao vivo do montador (docs/07 §3). */
@Injectable({ providedIn: 'root' })
export class PreparationsService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/preparations`;

  list(page = 1): Observable<PagedResult<Preparation>> {
    const params = new HttpParams().set('page', page).set('pageSize', PAGE_SIZE);
    return this.http
      .get<PagedResult<Preparation> | Preparation[]>(this.base, { params })
      .pipe(map(normalizePage));
  }

  /** Detalhe com a composição completa (usado ao editar um preparo). */
  get(id: string): Observable<Preparation> {
    return this.http.get<Preparation>(`${this.base}/${id}`);
  }

  create(body: PreparationRequest): Observable<Preparation> {
    return this.http.post<Preparation>(this.base, body);
  }

  update(id: string, body: PreparationRequest): Observable<Preparation> {
    return this.http.put<Preparation>(`${this.base}/${id}`, body);
  }

  remove(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }

  /**
   * Cálculo ao vivo do montador (endpoint acordado com o backend):
   * POST /preparations/calculate-preview com o form atual, sem salvar.
   */
  calculatePreview(body: PreparationRequest): Observable<PreparationCalculation> {
    return this.http.post<PreparationCalculation>(`${this.base}/calculate-preview`, body);
  }
}
