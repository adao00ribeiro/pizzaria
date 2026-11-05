import { BreakpointObserver, Breakpoints } from '@angular/cdk/layout';
import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

import { SystemAdminService } from '../../../core/services/system-admin.service';
import { ThemeService } from '../../../core/services/theme.service';

/**
 * Shell / Layout exclusivo do Administrador do Sistema (Governança Global).
 * Provê Sidebar de navegação, alternador de temas claro/escuro e cabeçalho de perfil.
 */
@Component({
  selector: 'pz-system-admin-shell',
  standalone: true,
  imports: [
    CommonModule,
    MatSidenavModule,
    MatToolbarModule,
    MatListModule,
    MatIconModule,
    MatButtonModule,
    MatTooltipModule,
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
  ],
  templateUrl: './system-admin-shell.component.html',
  styleUrl: './system-admin-shell.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SystemAdminShellComponent {
  private readonly router = inject(Router);
  private readonly breakpointObserver = inject(BreakpointObserver);

  readonly adminService = inject(SystemAdminService);
  readonly theme = inject(ThemeService);

  readonly wide = signal(false);
  readonly opened = signal(true);

  constructor() {
    this.breakpointObserver
      .observe([Breakpoints.Medium, Breakpoints.Large, Breakpoints.XLarge])
      .pipe(takeUntilDestroyed())
      .subscribe((state) => {
        const isWide = state.matches;
        this.wide.set(isWide);
        this.opened.set(isWide);
      });
  }

  onNavClick(): void {
    if (!this.wide()) {
      this.opened.set(false);
    }
  }

  logout(): void {
    this.adminService.logout();
    this.router.navigate(['/system-admin/login']);
  }
}
