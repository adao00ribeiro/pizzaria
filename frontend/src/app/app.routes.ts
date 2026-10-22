import { Routes } from '@angular/router';

import { authGuard } from './core/guards/auth.guard';
import { roleGuard } from './core/guards/role.guard';
import { tenantGuard } from './core/guards/tenant.guard';

export const routes: Routes = [
  { path: 'login', loadComponent: () => import('./layout/pages/login/login.component').then(m => m.LoginComponent) },
  { path: 'register', loadComponent: () => import('./layout/pages/register/register.component').then(m => m.RegisterComponent) },
  { path: 'menu/:slug', loadComponent: () => import('./features/public/public-menu.page').then(m => m.PublicMenuPage) },
  { path: 'checkout/:slug', loadComponent: () => import('./features/public/public-checkout.page').then(m => m.PublicCheckoutPage) },
  { path: 'acompanhar/:slug/:code', loadComponent: () => import('./features/public/public-tracking.page').then(m => m.PublicTrackingPage) },
  {
    path: '',
    loadComponent: () => import('./layout/shell/shell.component').then(m => m.ShellComponent),
    canActivate: [authGuard],
    children: [
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
      { path: 'dashboard', canActivate: [roleGuard('owner'), tenantGuard], loadComponent: () => import('./features/dashboard/dashboard.page').then(m => m.DashboardPage) },
      { path: 'pedidos', canActivate: [roleGuard('owner', 'atendente', 'garcom')], loadComponent: () => import('./features/orders/orders.page').then(m => m.OrdersPage) },
      { path: 'orders', redirectTo: 'pedidos', pathMatch: 'full' },
      { path: 'pedidos/:id/ticket', canActivate: [roleGuard('owner', 'atendente', 'garcom')], loadComponent: () => import('./features/orders/order-ticket.page').then(m => m.OrderTicketPage) },
      { path: 'whatsapp', canActivate: [roleGuard('owner', 'atendente'), tenantGuard], loadComponent: () => import('./features/whatsapp/whatsapp.page').then(m => m.WhatsAppPage) },
      { path: 'producao', canActivate: [roleGuard('owner', 'pizzaiolo')], loadComponent: () => import('./features/kitchen/kitchen.page').then(m => m.KitchenPage) },
      { path: 'kitchen', redirectTo: 'producao', pathMatch: 'full' },
      { path: 'lucro', canActivate: [roleGuard('owner')], loadComponent: () => import('./features/profit/profit.page').then(m => m.ProfitPage) },
      { path: 'profit', redirectTo: 'lucro', pathMatch: 'full' },
      { path: 'ingredients', canActivate: [roleGuard('owner')], loadComponent: () => import('./features/ingredients/ingredients.page').then(m => m.IngredientsPage) },
      { path: 'preparations', canActivate: [roleGuard('owner')], loadComponent: () => import('./features/preparations/preparations.page').then(m => m.PreparationsPage) },
      { path: 'fixed-costs', canActivate: [roleGuard('owner')], loadComponent: () => import('./features/fixed-costs/fixed-costs.page').then(m => m.FixedCostsPage) },
      { path: 'fees', canActivate: [roleGuard('owner')], loadComponent: () => import('./features/fees/fees.page').then(m => m.FeesPage) },
      { path: 'team', canActivate: [roleGuard('owner')], loadComponent: () => import('./features/team/team.page').then(m => m.TeamPage) },
      { path: 'products', canActivate: [roleGuard('owner')], loadComponent: () => import('./features/products/products.page').then(m => m.ProductsPage) },
      { path: 'sheets', canActivate: [roleGuard('owner')], loadComponent: () => import('./features/technical-sheets/sheets.page').then(m => m.SheetsPage) },
      { path: 'sheets/new', canActivate: [roleGuard('owner')], loadComponent: () => import('./features/technical-sheets/sheet-editor.page').then(m => m.SheetEditorPage) },
      { path: 'sheets/:id/edit', canActivate: [roleGuard('owner')], loadComponent: () => import('./features/technical-sheets/sheet-editor.page').then(m => m.SheetEditorPage) },
      { path: 'pricing/half-half', canActivate: [roleGuard('owner')], loadComponent: () => import('./features/pricing/half-half.page').then(m => m.HalfHalfPage) },
      { path: 'pricing/combos', canActivate: [roleGuard('owner')], loadComponent: () => import('./features/pricing/combos.page').then(m => m.CombosPage) },
      { path: 'pricing/beverages', canActivate: [roleGuard('owner')], loadComponent: () => import('./features/pricing/beverages.page').then(m => m.BeveragesPage) },
      { path: 'stock', canActivate: [roleGuard('owner', 'pizzaiolo')], loadComponent: () => import('./features/stock/stock.page').then(m => m.StockPage) },
      { path: 'alerts', canActivate: [roleGuard('owner')], loadComponent: () => import('./features/alerts/alerts.page').then(m => m.AlertsPage) },
      { path: '404', loadComponent: () => import('./layout/pages/not-found/not-found.component').then(m => m.NotFoundComponent) },
      { path: '**', redirectTo: 'dashboard' },
    ],
  },
  { path: '**', redirectTo: 'dashboard' },
];
