import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../../environments/environment';
import {
  NeighborhoodDto,
  NeighborhoodInputDto,
} from '../../core/models/neighborhood.models';

@Injectable({ providedIn: 'root' })
export class NeighborhoodsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/neighborhoods`;

  list(activeOnly?: boolean): Observable<NeighborhoodDto[]> {
    let params = new HttpParams();
    if (activeOnly !== undefined && activeOnly !== null) {
      params = params.set('activeOnly', activeOnly);
    }
    return this.http.get<NeighborhoodDto[]>(this.baseUrl, { params });
  }

  get(id: string): Observable<NeighborhoodDto> {
    return this.http.get<NeighborhoodDto>(`${this.baseUrl}/${id}`);
  }

  create(input: NeighborhoodInputDto): Observable<NeighborhoodDto> {
    return this.http.post<NeighborhoodDto>(this.baseUrl, input);
  }

  update(id: string, input: NeighborhoodInputDto): Observable<NeighborhoodDto> {
    return this.http.put<NeighborhoodDto>(`${this.baseUrl}/${id}`, input);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
