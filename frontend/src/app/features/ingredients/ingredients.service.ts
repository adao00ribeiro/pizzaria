import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';

import { environment } from '../../../environments/environment';
import { PAGE_SIZE, PagedResult } from '../../core/models/common.models';
import { Ingredient, IngredientRequest, IngredientUsages } from '../../core/models/ingredient.models';
import { normalizePage } from '../../core/utils/page.util';

/** CRUD de ingredientes (docs/07 §2). */
@Injectable({ providedIn: 'root' })
export class IngredientsService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/ingredients`;

  list(params: { page?: number; search?: string; pageSize?: number }): Observable<PagedResult<Ingredient>> {
    let httpParams = new HttpParams()
      .set('page', params.page ?? 1)
      .set('pageSize', params.pageSize ?? PAGE_SIZE);
    if (params.search) {
      httpParams = httpParams.set('search', params.search);
    }
    return this.http
      .get<PagedResult<Ingredient> | Ingredient[]>(this.base, { params: httpParams })
      .pipe(map(normalizePage));
  }

  get(id: string): Observable<Ingredient> {
    return this.http.get<Ingredient>(`${this.base}/${id}`);
  }

  create(body: IngredientRequest): Observable<Ingredient> {
    return this.http.post<Ingredient>(this.base, body);
  }

  update(id: string, body: IngredientRequest): Observable<Ingredient> {
    return this.http.put<Ingredient>(`${this.base}/${id}`, body);
  }

  remove(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }

  /** Preparos e fichas que usam o ingrediente (exibido em conflito 409). */
  usages(id: string): Observable<IngredientUsages> {
    return this.http.get<IngredientUsages>(`${this.base}/${id}/usages`);
  }
}
