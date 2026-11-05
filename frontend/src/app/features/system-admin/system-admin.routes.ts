import { Routes } from '@angular/router';

import { systemAdminGuard } from '../../core/guards/system-admin.guard';

export const SYSTEM_ADMIN_ROUTES: Routes = [
  { path: '', redirectTo: 'tenants', pathMatch: 'full' },
  {
    path: 'login',
    loadComponent: () =>
      import('./pages/system-admin-login.page').then((m) => m.SystemAdminLoginPage),
  },
  {
    path: 'reset-password',
    loadComponent: () =>
      import('./pages/system-admin-reset-password.page').then((m) => m.SystemAdminResetPasswordPage),
  },
  {
    path: '',
    canActivate: [systemAdminGuard],
    loadComponent: () =>
      import('./layout/system-admin-shell.component').then((m) => m.SystemAdminShellComponent),
    children: [
      {
        path: 'tenants',
        loadComponent: () =>
          import('./pages/tenants-list.page').then((m) => m.TenantsListPage),
      },
    ],
  },
  { path: '**', redirectTo: 'tenants' },
];
