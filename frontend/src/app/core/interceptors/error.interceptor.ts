import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { catchError, throwError } from 'rxjs';

import { ApiError } from '../models/common.models';
import { SILENT_ERROR } from './http-context';

function isApiError(body: unknown): body is ApiError {
  return (
    !!body &&
    typeof body === 'object' &&
    typeof (body as ApiError).message === 'string'
  );
}

/** Monta uma mensagem clara em português a partir do erro padronizado da API. */
export function extractApiErrorMessage(err: HttpErrorResponse): string {
  const body = err.error;

  if (isApiError(body)) {
    const details = Array.isArray(body.errors)
      ? body.errors
          .filter((e) => !!e?.field && !!e?.detail)
          .map((e) => `${e.field}: ${e.detail}`)
      : [];
    if (details.length > 0) {
      const joined = details.join(' • ');
      return body.message ? `${body.message} ${joined}` : joined;
    }
    return body.message;
  }

  if (typeof body === 'string' && body.trim().length > 0) {
    return body;
  }

  switch (err.status) {
    case 0:
      return 'Não foi possível conectar ao servidor. Verifique sua conexão e tente novamente.';
    case 403:
      return 'Você não tem permissão para executar esta ação.';
    case 404:
      return 'Registro não encontrado.';
    case 409:
      return 'Operação conflitante: o registro está em uso e não pode ser alterado.';
    case 429:
      return 'Muitas tentativas. Aguarde um instante e tente novamente.';
    case 500:
    case 502:
    case 503:
    case 504:
      return 'Erro no servidor. Tente novamente em instantes.';
    default:
      return `Ocorreu um erro inesperado (HTTP ${err.status}).`;
  }
}

/**
 * Converte erros HTTP da API em snackbars em português (NF-U06).
 * 401 fica por conta do JwtInterceptor (refresh/logout) e requisições
 * marcadas com SILENT_ERROR são tratadas pela própria tela.
 */
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const snack = inject(MatSnackBar);

  return next(req).pipe(
    catchError((err) => {
      if (err instanceof HttpErrorResponse) {
        const silent = req.context.get(SILENT_ERROR);
        // 401 é do JwtInterceptor; 409 (conflito de exclusão) é tratado
        // pela própria tela, que mostra a mensagem específica do backend.
        if (!silent && err.status !== 401 && err.status !== 409) {
          snack.open(extractApiErrorMessage(err), 'Fechar', {
            duration: 6000,
            panelClass: 'error-snack',
          });
        }
      }
      return throwError(() => err);
    }),
  );
};
