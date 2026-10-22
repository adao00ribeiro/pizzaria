import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../../environments/environment';
import {
  ProfitByChannelDto,
  ProfitByDayDto,
  ProfitByProductDto,
  ProfitReportDto,
} from '../../core/models/profit.models';

@Injectable({ providedIn: 'root' })
export class ProfitService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/reports/profit`;

  summary(from: string, to: string): Observable<ProfitReportDto> {
    const params = this.buildParams(from, to);
    return this.http.get<ProfitReportDto>(this.base, { params });
  }

  byDay(from: string, to: string): Observable<ProfitByDayDto[]> {
    const params = this.buildParams(from, to);
    return this.http.get<ProfitByDayDto[]>(`${this.base}/by-day`, { params });
  }

  byProduct(from: string, to: string): Observable<ProfitByProductDto[]> {
    const params = this.buildParams(from, to);
    return this.http.get<ProfitByProductDto[]>(`${this.base}/by-product`, { params });
  }

  byChannel(from: string, to: string): Observable<ProfitByChannelDto[]> {
    const params = this.buildParams(from, to);
    return this.http.get<ProfitByChannelDto[]>(`${this.base}/by-channel`, { params });
  }

  private buildParams(from: string, to: string): HttpParams {
    let params = new HttpParams();
    if (from) params = params.set('from', from);
    if (to) params = params.set('to', to);
    return params;
  }
}
