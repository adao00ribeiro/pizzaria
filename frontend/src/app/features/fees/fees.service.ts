import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../../environments/environment';
import { Fees } from '../../core/models/cost.models';

/** Configuração única de taxas do tenant (GET/PUT /fees — docs/07 §4). */
@Injectable({ providedIn: 'root' })
export class FeesService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiBaseUrl}/fees`;

  get(): Observable<Fees> {
    return this.http.get<Fees>(this.url);
  }

  update(body: Fees): Observable<Fees> {
    return this.http.put<Fees>(this.url, body);
  }
}
