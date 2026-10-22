import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../../environments/environment';
import { CreateUserRequest, UpdateUserRequest, User } from '../../core/models/user.models';

/** Gestão de equipe: usuários e papéis do tenant (docs/10, Sprint 1 — só owner). */
@Injectable({ providedIn: 'root' })
export class TeamService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/users`;

  list(): Observable<User[]> {
    return this.http.get<User[]>(this.base);
  }

  create(body: CreateUserRequest): Observable<User> {
    return this.http.post<User>(this.base, body);
  }

  update(id: string, body: UpdateUserRequest): Observable<User> {
    return this.http.put<User>(`${this.base}/${id}`, body);
  }

  remove(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }
}
