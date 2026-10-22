import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Observable, finalize, shareReplay, tap } from 'rxjs';

import { environment } from '../../../environments/environment';
import { TenantInfo } from '../models/tenant.models';
import { StorageService } from './storage.service';

/** GET/PUT /tenant — dados da pizzaria (nome + slug público, docs/10 Sprint 1). */
@Injectable({ providedIn: 'root' })
export class TenantService {
  private readonly http = inject(HttpClient);
  private readonly storage = inject(StorageService);

  readonly name = signal<string>(this.storage.getPizzeriaName() ?? '');
  readonly slug = signal<string | null>(null);
  readonly loaded = signal(false);

  private readonly url = `${environment.apiBaseUrl}/tenant`;
  private request$: Observable<TenantInfo> | null = null;

  /** Carrega (uma única vez, compartilhando a requisição) o nome e o slug da pizzaria. */
  ensureLoaded(): Observable<TenantInfo> {
    if (this.loaded()) {
      return new Observable<TenantInfo>((observer) => {
        observer.next({ name: this.name(), slug: this.slug() });
        observer.complete();
      });
    }
    if (!this.request$) {
      this.request$ = this.http.get<TenantInfo>(this.url).pipe(
        tap((res) => this.apply(res)),
        finalize(() => {
          this.request$ = null;
        }),
        shareReplay(1),
      );
    }
    return this.request$;
  }

  /** Força a recarga dos dados do tenant (usado na tela de taxas/slug). */
  reload(): Observable<TenantInfo> {
    this.loaded.set(false);
    this.request$ = null;
    return this.ensureLoaded();
  }

  /** Atualiza nome e/ou slug (owner). */
  update(input: { name?: string; slug?: string }): Observable<TenantInfo> {
    return this.http.put<TenantInfo>(this.url, input).pipe(tap((res) => this.apply(res)));
  }

  private apply(res: TenantInfo): void {
    if (res?.name) {
      this.name.set(res.name);
      this.storage.setPizzeriaName(res.name);
    }
    this.slug.set(res?.slug ?? null);
    this.loaded.set(true);
  }
}
