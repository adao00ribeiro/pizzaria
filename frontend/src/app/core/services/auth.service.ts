import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, finalize, shareReplay, tap, throwError } from 'rxjs';

import { environment } from '../../../environments/environment';
import { AuthResponse, LoginRequest, RegisterRequest } from '../models/auth.models';
import { StorageService } from './storage.service';

/**
 * Autenticação (login, cadastro, refresh do JWT) e estado de sessão.
 * O refresh é compartilhado: chamadas simultâneas em 401 aguardam o mesmo POST.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly storage = inject(StorageService);
  private readonly router = inject(Router);

  private refreshInFlight$: Observable<AuthResponse> | null = null;

  readonly accessToken = signal<string | null>(this.storage.getAccessToken());
  readonly isAuthenticated = computed(() => !!this.accessToken());
  readonly pizzeriaName = signal<string>(this.storage.getPizzeriaName() ?? '');

  /**
   * Papel principal do usuário (owner | atendente | pizzaiolo), lido do claim
   * `role` do JWT (docs/10, Sprint 1). `null` quando não há sessão.
   */
  readonly role = signal<string | null>(this.readRole());

  readonly isOwner = computed(() => this.role() === 'owner');

  private readonly base = `${environment.apiBaseUrl}/auth`;

  register(req: RegisterRequest): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(`${this.base}/register`, req)
      .pipe(tap((res) => this.saveSession(res)));
  }

  login(req: LoginRequest): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(`${this.base}/login`, req)
      .pipe(tap((res) => this.saveSession(res)));
  }

  /** Verifica se o usuário logado está em um dos papéis informados. */
  isInRole(...roles: string[]): boolean {
    const current = this.role();
    return !!current && roles.includes(current);
  }

  /** Rota inicial apropriada ao papel (docs/10, Sprint 1). */
  homeRouteForRole(): string {
    switch (this.role()) {
      case 'pizzaiolo':
        return '/producao';
      case 'atendente':
      case 'garcom':
        return '/pedidos';
      default:
        return '/dashboard';
    }
  }

  /** POST /auth/refresh — deduplicado entre requisições concorrentes. */
  refresh(): Observable<AuthResponse> {
    if (!this.refreshInFlight$) {
      const refreshToken = this.storage.getRefreshToken();
      if (!refreshToken) {
        return throwError(() => ({ status: 401 }) as unknown);
      }
      this.refreshInFlight$ = this.http
        .post<AuthResponse>(`${this.base}/refresh`, { refreshToken })
        .pipe(
          tap((res) => this.saveSession(res)),
          finalize(() => {
            this.refreshInFlight$ = null;
          }),
          shareReplay(1),
        );
    }
    return this.refreshInFlight$;
  }

  logout(): void {
    this.clearSession();
    this.router.navigate(['/login']);
  }

  refreshTokenExists(): boolean {
    return !!this.storage.getRefreshToken();
  }

  private saveSession(res: AuthResponse): void {
    this.storage.saveSession(res);
    this.accessToken.set(res.accessToken);
    this.role.set(this.readRole(res.accessToken));
  }

  private clearSession(): void {
    this.storage.clearSession();
    this.accessToken.set(null);
    this.role.set(null);
    this.pizzeriaName.set('');
    this.refreshInFlight$ = null;
  }

  /** Decodifica o payload do JWT (base64url) e lê o claim `role`. */
  private readRole(token?: string | null): string | null {
    const raw = token ?? this.storage.getAccessToken();
    if (!raw) {
      return null;
    }
    try {
      const payload = raw.split('.')[1];
      if (!payload) {
        return null;
      }
      const base64 = payload.replace(/-/g, '+').replace(/_/g, '/');
      const padded = base64.padEnd(Math.ceil(base64.length / 4) * 4, '=');
      const json = atob(padded);
      const claims = JSON.parse(json) as Record<string, unknown>;
      const claim = claims['role'];
      if (typeof claim === 'string') {
        return claim;
      }
      if (Array.isArray(claim) && typeof claim[0] === 'string') {
        return claim[0];
      }
      // Fallback: role_list ("owner,atendente")
      const list = claims['role_list'];
      if (typeof list === 'string' && list.length > 0) {
        return list.split(',')[0];
      }
      return null;
    } catch {
      return null;
    }
  }
}
