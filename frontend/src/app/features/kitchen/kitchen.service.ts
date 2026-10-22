import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../../environments/environment';
import { KitchenQueueResponse } from '../../core/models/kitchen.models';

/**
 * Serviço do painel da produção (`pizzaiolo` e `owner`) — docs/07 §17, docs/10 Sprint 3.
 * Erros de rede/validação passam pelo `ErrorInterceptor` padrão da aplicação.
 */
@Injectable({ providedIn: 'root' })
export class KitchenService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/kitchen/queue`;

  /** Consulta a fila ativa (Recebido → Em preparo → Pronto) com contagens e receita. */
  queue(): Observable<KitchenQueueResponse> {
    return this.http.get<KitchenQueueResponse>(this.base);
  }

  /** Move o pedido de "Recebido" para "Em preparo". */
  start(orderId: string): Observable<unknown> {
    return this.http.post(`${this.base}/${orderId}/start`, {});
  }

  /** Move o pedido de "Em preparo" para "Pronto" (dispara baixa idempotente no estoque). */
  ready(orderId: string): Observable<unknown> {
    return this.http.post(`${this.base}/${orderId}/ready`, {});
  }

  /** Despacha/conclui o pedido "Pronto" (Delivery → Saiu; demais → Concluído), tirando-o da fila. */
  finish(orderId: string): Observable<unknown> {
    return this.http.post(`${this.base}/${orderId}/finish`, {});
  }

  /** Cancela um pedido com motivo obrigatório (estorna estoque caso já tenha sido baixado). */
  cancel(orderId: string, reason: string): Observable<unknown> {
    return this.http.post(`${this.base}/${orderId}/cancel`, { reason });
  }
}
