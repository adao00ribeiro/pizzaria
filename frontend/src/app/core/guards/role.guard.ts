import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { AuthService } from '../services/auth.service';

/**
 * Restringe a rota a um ou mais papéis (docs/10, Sprint 1).
 * Sessão inválida vai para /login; papel errado cai na home do próprio papel.
 *
 * Uso: `canActivate: [roleGuard('owner')]` ou `roleGuard('owner', 'atendente')`.
 */
export const roleGuard =
  (...roles: string[]): CanActivateFn =>
  () => {
    const auth = inject(AuthService);
    const router = inject(Router);

    if (!auth.isAuthenticated() && !auth.refreshTokenExists()) {
      return router.createUrlTree(['/login']);
    }

    // Papel ainda desconhecido (token legado sem claim): deixa o backend decidir.
    if (!auth.role()) {
      return true;
    }

    return auth.isInRole(...roles) ? true : router.createUrlTree([auth.homeRouteForRole()]);
  };
