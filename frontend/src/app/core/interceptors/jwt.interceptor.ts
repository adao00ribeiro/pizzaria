import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError } from 'rxjs';

import { AuthService } from '../services/auth.service';

/**
 * Adiciona `Authorization: Bearer …` a todas as requisições.
 * Em 401 tenta renovar o token (POST /auth/refresh) e repetir a requisição
 * uma única vez; se o refresh falhar, encerra a sessão e vai para o login.
 */
export const jwtInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const snack = inject(MatSnackBar);
  const router = inject(Router);

  const token = auth.accessToken();
  const authReq = token
    ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
    : req;

  return next(authReq).pipe(
    catchError((err) => {
      const isUnauthorized = err instanceof HttpErrorResponse && err.status === 401;
      const isAuthEndpoint = req.url.includes('/auth/');

      if (!isUnauthorized || isAuthEndpoint) {
        return throwError(() => err);
      }

      return auth.refresh().pipe(
        switchMap(() => {
          const newToken = auth.accessToken();
          const retry = newToken
            ? req.clone({ setHeaders: { Authorization: `Bearer ${newToken}` } })
            : req;
          return next(retry);
        }),
        catchError((refreshErr) => {
          if (auth.isAuthenticated() || auth.refreshTokenExists()) {
            auth.logout();
          }
          snack.open('Sessão expirada. Faça login novamente.', 'Fechar', {
            duration: 5000,
            panelClass: 'error-snack',
          });
          router.navigate(['/login']);
          return throwError(() => refreshErr);
        }),
      );
    }),
  );
};
