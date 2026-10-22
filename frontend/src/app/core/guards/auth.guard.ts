import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { AuthService } from '../services/auth.service';

/** Exige sessão autenticada; caso contrário redireciona para /login. */
export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return auth.isAuthenticated() || auth.refreshTokenExists()
    ? true
    : router.createUrlTree(['/login']);
};
