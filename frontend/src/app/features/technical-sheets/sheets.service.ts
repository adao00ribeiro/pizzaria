import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';

import { environment } from '../../../environments/environment';
import { MarginStatus, PAGE_SIZE, PagedResult } from '../../core/models/common.models';
import { SheetDetail, SheetListItem, SheetSaveRequest } from '../../core/models/sheet.models';
import { normalizePage } from '../../core/utils/page.util';

/** CRUD de fichas técnicas (docs/07 §6). */
@Injectable({ providedIn: 'root' })
export class SheetsService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/technicalsheets`;

  list(params: { status?: MarginStatus | ''; page?: number }): Observable<PagedResult<SheetListItem>> {
    let httpParams = new HttpParams().set('page', params.page ?? 1).set('pageSize', PAGE_SIZE);
    if (params.status) {
      httpParams = httpParams.set('status', params.status);
    }
    return this.http
      .get<PagedResult<SheetListItem> | SheetListItem[]>(this.base, { params: httpParams })
      .pipe(map(normalizePage));
  }

  get(id: string): Observable<SheetDetail> {
    return this.http.get<SheetDetail>(`${this.base}/${id}`);
  }

  create(body: SheetSaveRequest): Observable<SheetDetail> {
    return this.http.post<SheetDetail>(this.base, body);
  }

  update(id: string, body: SheetSaveRequest): Observable<SheetDetail> {
    return this.http.put<SheetDetail>(`${this.base}/${id}`, body);
  }

  remove(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }

  /** Força o recálculo completo no servidor e devolve o resultado. */
  calculate(id: string): Observable<unknown> {
    return this.http.post(`${this.base}/${id}/calculate`, {});
  }
}
