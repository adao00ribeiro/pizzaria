import { BreakpointObserver, Breakpoints } from '@angular/cdk/layout';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatBadgeModule } from '@angular/material/badge';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { catchError, filter, of, switchMap } from 'rxjs';

import { AuthService } from '../../core/services/auth.service';
import { TenantService } from '../../core/services/tenant.service';
import { ThemeService } from '../../core/services/theme.service';
import { AlertsService } from '../../features/alerts/alerts.service';
import { StockService } from '../../features/stock/stock.service';

interface MenuLink {
  path: string;
  label: string;
  icon: string;
  /** Desativa o destaque ativo (ex.: item que abre a mesma rota em outra aba) */
  active?: boolean;
  /** Papéis que enxergam o item; ausente = todos os papéis (docs/10, Sprint 1). */
  roles?: string[];
}

/**
 * Shell do sistema: menu lateral categorizado e organizado + toolbar com nome da
 * pizzaria, alertas com badge no header, chip de perfil e logout.
 */
@Component({
  selector: 'pz-shell',
  standalone: true,
  imports: [
    MatSidenavModule,
    MatToolbarModule,
    MatListModule,
    MatIconModule,
    MatButtonModule,
    MatBadgeModule,
    MatTooltipModule,
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
  ],
  templateUrl: './shell.component.html',
  styleUrl: './shell.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ShellComponent {
  private readonly router = inject(Router);
  private readonly alertsService = inject(AlertsService);
  private readonly stockService = inject(StockService);

  readonly auth = inject(AuthService);
  readonly tenant = inject(TenantService);
  readonly theme = inject(ThemeService);

  /** Lateral fixa em telas largas; gaveta no celular (NF-U02). */
  readonly wide = signal(false);
  readonly opened = signal(true);
  readonly precificacaoOpen = signal(true);
  readonly cadastrosOpen = signal(true);
  readonly alertCount = signal(0);
  readonly lowStockCount = signal(0);

  /** Seção 2: Operação diária (pedidos, atendimento whatsapp, cozinha, estoque e lucro real). */
  readonly operacaoLinks: MenuLink[] = [
    { path: '/pedidos', label: 'Pedidos', icon: 'receipt_long', roles: ['owner', 'atendente', 'garcom'] },
    { path: '/whatsapp', label: 'WhatsApp / Atendimento', icon: 'forum', roles: ['owner', 'atendente'] },
    { path: '/producao', label: 'Produção / Cozinha', icon: 'outdoor_grill', roles: ['owner', 'pizzaiolo'] },
    { path: '/stock', label: 'Estoque', icon: 'inventory_2', roles: ['owner', 'pizzaiolo'] },
    { path: '/lucro', label: 'Lucro Real', icon: 'trending_up', roles: ['owner'] },
    { path: '/ia', label: 'Assistente IA', icon: 'auto_awesome', roles: ['owner'] },
  ];

  /** Seção 3: Cardápio e precificação (gestão de produtos, combos e fichas técnicas). */
  readonly precificacaoLinks: MenuLink[] = [
    { path: '/sheets', label: 'Fichas técnicas', icon: 'description' },
    { path: '/pricing/half-half', label: 'Meio a meio / 4 sabores', icon: 'local_pizza' },
    { path: '/pricing/combos', label: 'Combos', icon: 'redeem' },
    { path: '/pricing/beverages', label: 'Bebidas', icon: 'local_drink' },
  ];

  /** Seção 4: Cadastros e configurações (ingredientes, custos, taxas e equipe). */
  readonly cadastrosLinks: MenuLink[] = [
    { path: '/ingredients', label: 'Ingredientes', icon: 'shopping_basket' },
    { path: '/preparations', label: 'Preparos', icon: 'kitchen' },
    { path: '/products', label: 'Produtos & Tamanhos', icon: 'restaurant_menu' },
    { path: '/neighborhoods', label: 'Bairros & Regiões', icon: 'location_on', roles: ['owner'] },
    { path: '/fixed-costs', label: 'Custos fixos', icon: 'account_balance_wallet' },
    { path: '/fees', label: 'Taxas', icon: 'percent' },
    { path: '/team', label: 'Equipe', icon: 'group', roles: ['owner'] },
  ];

  /** Rótulo amigável do perfil do usuário logado para exibição no header. */
  readonly roleLabel = computed(() => {
    const role = this.auth.role();
    switch (role) {
      case 'owner':
        return 'Administrador';
      case 'atendente':
        return 'Atendente / Caixa';
      case 'pizzaiolo':
        return 'Pizzaiolo';
      case 'garcom':
        return 'Garçom';
      default:
        return 'Usuário';
    }
  });

  /** Ícone representativo do perfil do usuário logado. */
  readonly roleIcon = computed(() => {
    const role = this.auth.role();
    switch (role) {
      case 'owner':
        return 'admin_panel_settings';
      case 'atendente':
        return 'support_agent';
      case 'pizzaiolo':
        return 'outdoor_grill';
      case 'garcom':
        return 'room_service';
      default:
        return 'account_circle';
    }
  });

  /** Filtra os links visíveis para o papel do usuário logado. */
  visible(links: MenuLink[]): MenuLink[] {
    const role = this.auth.role();
    return links.filter((link) => !link.roles || (!!role && link.roles.includes(role)));
  }

  /** Precificação e cadastros são exclusivos do owner (custos e margens). */
  readonly showsManagement = () => this.auth.isOwner();

  readonly showsAlerts = () => this.auth.isOwner();

  readonly showsStock = () => this.auth.isOwner() || this.auth.isInRole('pizzaiolo');

  constructor() {
    inject(BreakpointObserver)
      .observe([Breakpoints.Handset])
      .pipe(takeUntilDestroyed())
      .subscribe((state) => {
        // Em telas pequenas (celular) a vira gaveta e começa fechada
        this.wide.set(!state.matches);
        this.opened.set(!state.matches);
      });

    const nav$ = this.router.events.pipe(
      filter((e): e is NavigationEnd => e instanceof NavigationEnd),
      takeUntilDestroyed(),
    );

    nav$
      .pipe(
        switchMap(() =>
          this.auth.isOwner()
            ? this.alertsService.count().pipe(catchError(() => of({ count: 0 })))
            : of({ count: 0 }),
        ),
      )
      .subscribe((res) => this.alertCount.set(res.count ?? 0));

    nav$
      .pipe(
        switchMap(() =>
          this.showsStock()
            ? this.stockService
                .balances({ belowMinimum: true, page: 1 })
                .pipe(catchError(() => of({ items: [], total: 0 })))
            : of({ items: [], total: 0 }),
        ),
      )
      .subscribe((res) => this.lowStockCount.set(res.total ?? 0));
  }

  toggleGroup(group: 'precificacao' | 'cadastros'): void {
    if (group === 'precificacao') {
      this.precificacaoOpen.update((v) => !v);
    } else {
      this.cadastrosOpen.update((v) => !v);
    }
  }

  /** Fecha a gaveta após navegar quando o menu está sobreposto (celular). */
  onNavClick(): void {
    if (!this.wide()) {
      this.opened.set(false);
    }
  }
}
