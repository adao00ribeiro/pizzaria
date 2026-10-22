import { HttpClient, HttpContext, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';

import { environment } from '../../../environments/environment';
import { AlertCount, Alert } from '../../core/models/alert.models';
import { AlertStatus, AlertType, PAGE_SIZE, PagedResult } from '../../core/models/common.models';
import { normalizePage } from '../../core/utils/page.util';
import { SILENT_ERROR } from '../../core/interceptors/http-context';

/** Serviço de alertas de margem/estoque (docs/07 §11). */
@Injectable({ providedIn: 'root' })
export class AlertsService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/alerts`;

  list(params: {
    status?: AlertStatus | '';
    type?: AlertType | '';
    page?: number;
  }): Observable<PagedResult<Alert>> {
    let httpParams = new HttpParams().set('page', params.page ?? 1).set('pageSize', PAGE_SIZE);
    if (params.status) {
      httpParams = httpParams.set('status', params.status);
    }
    if (params.type) {
      httpParams = httpParams.set('type', params.type);
    }
    return this.http
      .get<PagedResult<Alert> | Alert[]>(this.base, { params: httpParams })
      .pipe(map(normalizePage));
  }

  /** Contador do badge do menu — silencioso (não gera snackbar). */
  count(): Observable<AlertCount> {
    return this.http.get<AlertCount>(`${this.base}/count`, {
      context: new HttpContext().set(SILENT_ERROR, true),
    });
  }

  resolve(id: string): Observable<unknown> {
    return this.http.post(`${this.base}/${id}/resolve`, {});
  }
}
