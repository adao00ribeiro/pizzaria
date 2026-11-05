import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';

import { environment } from '../../../environments/environment';
import { StorageService } from './storage.service';

export interface TenantListItem {
  id: string;
  name: string;
  slug: string | null;
  createdAt: string;
  ownerName: string;
  ownerEmail: string;
  isActive: boolean;
  deactivatedAt: string | null;
  deactivationReason: string | null;
}

export interface PagedResult<T> {
  items: T[];
  total: number;
}

export interface SystemAdminAuthResponse {
  token: string;
  expiresAt: string;
  email: string;
  fullName: string;
}

export interface TenantStatusUpdateInput {
  isActive: boolean;
  reason?: string | null;
}

export interface SystemAdminLoginInput {
  email: string;
  password: string;
}

export interface ForgotPasswordInput {
  email: string;
}

export interface ResetPasswordInput {
  token: string;
  newPassword: string;
}

@Injectable({ providedIn: 'root' })
export class SystemAdminService {
  private readonly http = inject(HttpClient);
  private readonly storage = inject(StorageService);
  private readonly router = inject(Router);

  private readonly base = `${environment.apiBaseUrl}/system-admin`;

  readonly accessToken = signal<string | null>(this.storage.getSystemAdminToken());
  readonly currentUser = signal<{ email: string; fullName: string } | null>(
    this.storage.getSystemAdminUser()
  );

  readonly isAuthenticated = computed(() => {
    const token = this.accessToken();
    if (!token) return false;
    return this.hasSystemAdminClaim(token);
  });

  login(input: SystemAdminLoginInput): Observable<SystemAdminAuthResponse> {
    return this.http
      .post<SystemAdminAuthResponse>(`${this.base}/auth/login`, input)
      .pipe(
        tap((res) => {
          this.storage.saveSystemAdminSession(res.token, {
            email: res.email,
            fullName: res.fullName,
          });
          this.accessToken.set(res.token);
          this.currentUser.set({ email: res.email, fullName: res.fullName });
        })
      );
  }

  forgotPassword(input: ForgotPasswordInput): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.base}/auth/forgot-password`, input);
  }

  resetPassword(input: ResetPasswordInput): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.base}/auth/reset-password`, input);
  }

  getTenants(
    page: number = 1,
    pageSize: number = 15,
    search?: string,
    isActive?: boolean
  ): Observable<PagedResult<TenantListItem>> {
    let params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());

    if (search && search.trim()) {
      params = params.set('search', search.trim());
    }

    if (isActive !== undefined && isActive !== null) {
      params = params.set('isActive', isActive.toString());
    }

    return this.http.get<PagedResult<TenantListItem>>(`${this.base}/tenants`, { params });
  }

  updateTenantStatus(
    tenantId: string,
    input: TenantStatusUpdateInput
  ): Observable<TenantListItem> {
    return this.http.patch<TenantListItem>(`${this.base}/tenants/${tenantId}/status`, input);
  }

  logout(): void {
    this.storage.clearSystemAdminSession();
    this.accessToken.set(null);
    this.currentUser.set(null);
    this.router.navigate(['/system-admin/login']);
  }

  private hasSystemAdminClaim(token: string): boolean {
    try {
      const payload = token.split('.')[1];
      if (!payload) return false;
      const base64 = payload.replace(/-/g, '+').replace(/_/g, '/');
      const padded = base64.padEnd(Math.ceil(base64.length / 4) * 4, '=');
      const json = atob(padded);
      const claims = JSON.parse(json) as Record<string, unknown>;
      return claims['system_admin'] === 'true' || claims['system_admin'] === true;
    } catch {
      return false;
    }
  }
}
