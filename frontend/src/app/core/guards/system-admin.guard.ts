import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { SystemAdminService } from '../services/system-admin.service';

/**
 * Exige sessão autenticada exclusiva do Administrador do Sistema (claim system_admin == true);
 * caso contrário, redireciona para /system-admin/login.
 */
export const systemAdminGuard: CanActivateFn = () => {
  const admin = inject(SystemAdminService);
  const router = inject(Router);

  return admin.isAuthenticated()
    ? true
    : router.createUrlTree(['/system-admin/login']);
};
