import { HttpClient, HttpContext } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { BehaviorSubject, Observable, finalize } from 'rxjs';

import { environment } from '../../../environments/environment';
import { SILENT_ERROR } from '../../core/interceptors/http-context';
import {
  ComboPreviewResult,
  MixedPricingRequest,
  MixedPricingResult,
  PricingResult,
} from '../../core/models/pricing.models';
import { PricingPreviewRequest } from '../../core/models/sheet.models';

/**
 * Cálculos de precificação on-demand (docs/07 §6-§8).
 * Mantém o estado "recalculando em andamento" (docs/08 §6) como
 * BehaviorSubject<boolean>, exposto também como signal.
 */
@Injectable({ providedIn: 'root' })
export class PricingService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/pricing`;

  private readonly recalculatingSubject = new BehaviorSubject<boolean>(false);
  /** Uso com async pipe: `recalculating$ | async` */
  readonly recalculating$ = this.recalculatingSubject.asObservable();

  private readonly _recalculating = signal(false);
  /** Uso em template com signal: `pricing.recalculating()` */
  readonly recalculating = this._recalculating.asReadonly();
  readonly recalculatingFlag = computed(() => this._recalculating());

  /** Preview ao vivo do montador de ficha (sem salvar). */
  preview(body: PricingPreviewRequest): Observable<PricingResult> {
    return this.track(
      this.http.post<PricingResult>(`${this.base}/preview`, body, {
        context: new HttpContext().set(SILENT_ERROR, true),
      }),
    );
  }

  /** Pizza meio a meio (2 sabores). */
  halfHalf(body: MixedPricingRequest): Observable<MixedPricingResult> {
    return this.track(
      this.http.post<MixedPricingResult>(`${this.base}/half-half`, body),
    );
  }

  /** Pizza de 4 sabores. */
  fourFlavors(body: MixedPricingRequest): Observable<MixedPricingResult> {
    return this.track(
      this.http.post<MixedPricingResult>(`${this.base}/four-flavors`, body),
    );
  }

  /** Preview de combo sem salvar. */
  comboPreview(body: unknown): Observable<ComboPreviewResult> {
    return this.track(
      this.http.post<ComboPreviewResult>(`${this.base}/combos/preview`, body, {
        context: new HttpContext().set(SILENT_ERROR, true),
      }),
    );
  }

  private track<T>(source: Observable<T>): Observable<T> {
    this.recalculatingSubject.next(true);
    this._recalculating.set(true);
    return source.pipe(
      finalize(() => {
        this.recalculatingSubject.next(false);
        this._recalculating.set(false);
      }),
    );
  }
}
