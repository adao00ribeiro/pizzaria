import { Injectable } from '@angular/core';

import { AuthResponse } from '../models/auth.models';

const KEYS = {
  accessToken: 'pz.accessToken',
  refreshToken: 'pz.refreshToken',
  expiresAt: 'pz.expiresAt',
  pizzeriaName: 'pz.pizzeriaName',
} as const;

/**
 * Persistência local da sessão (JWT) e do nome da pizzaria.
 * Envolvido em try/catch para não quebrar em navegadores com storage bloqueado.
 */
@Injectable({ providedIn: 'root' })
export class StorageService {
  private get store(): Storage | null {
    try {
      return typeof localStorage !== 'undefined' ? localStorage : null;
    } catch {
      return null;
    }
  }

  getAccessToken(): string | null {
    return this.get(KEYS.accessToken);
  }

  getRefreshToken(): string | null {
    return this.get(KEYS.refreshToken);
  }

  getExpiresAt(): string | null {
    return this.get(KEYS.expiresAt);
  }

  saveSession(res: AuthResponse): void {
    this.set(KEYS.accessToken, res.accessToken);
    this.set(KEYS.refreshToken, res.refreshToken);
    const expiresAt = new Date(Date.now() + res.expiresIn * 1000).toISOString();
    this.set(KEYS.expiresAt, expiresAt);
  }

  clearSession(): void {
    this.remove(KEYS.accessToken);
    this.remove(KEYS.refreshToken);
    this.remove(KEYS.expiresAt);
    this.remove(KEYS.pizzeriaName);
  }

  hasSession(): boolean {
    return !!this.getAccessToken() || !!this.getRefreshToken();
  }

  getPizzeriaName(): string | null {
    return this.get(KEYS.pizzeriaName);
  }

  setPizzeriaName(name: string): void {
    this.set(KEYS.pizzeriaName, name);
  }

  private get(key: string): string | null {
    try {
      return this.store?.getItem(key) ?? null;
    } catch {
      return null;
    }
  }

  private set(key: string, value: string): void {
    try {
      this.store?.setItem(key, value);
    } catch {
      /* storage indisponível — sessão fica apenas em memória */
    }
  }

  private remove(key: string): void {
    try {
      this.store?.removeItem(key);
    } catch {
      /* ignore */
    }
  }
}
