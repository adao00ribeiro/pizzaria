import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { catchError, map, of } from 'rxjs';

import { AuthService } from '../services/auth.service';
import { TenantService } from '../services/tenant.service';

/**
 * Garante que o tenant (dados da pizzaria) está carregado antes de abrir o
 * shell do sistema. Falhas de rede não bloqueiam a navegação (a toolbar
 * mostra o nome em cache ou o texto padrão).
 */
export const tenantGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const tenant = inject(TenantService);

  if (!auth.isAuthenticated() && !auth.refreshTokenExists()) {
    return router.createUrlTree(['/login']);
  }

  if (tenant.loaded()) {
    return true;
  }

  return tenant.ensureLoaded().pipe(
    map(() => true),
    // Em erro, segue para o shell mesmo assim (o JwtInterceptor cuida do 401)
    catchError(() => of(true)),
  );
};
