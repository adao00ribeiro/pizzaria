import { computed, Injectable, signal } from '@angular/core';

export type ThemeMode = 'light' | 'dark';

const THEME_STORAGE_KEY = 'pz_theme';

/**
 * Serviço responsável por gerenciar e alternar o tema da aplicação (Claro/Escuro).
 * Utiliza Angular Signals reativos e persiste a preferência do usuário no localStorage.
 */
@Injectable({
  providedIn: 'root',
})
export class ThemeService {
  readonly themeMode = signal<ThemeMode>('light');
  readonly isDark = computed(() => this.themeMode() === 'dark');

  constructor() {
    let initialMode: ThemeMode = 'light';
    try {
      const saved = localStorage.getItem(THEME_STORAGE_KEY) as ThemeMode | null;
      if (saved === 'dark' || saved === 'light') {
        initialMode = saved;
      } else if (
        typeof window !== 'undefined' &&
        window.matchMedia &&
        window.matchMedia('(prefers-color-scheme: dark)').matches
      ) {
        initialMode = 'dark';
      }
    } catch {
      // Falha silenciosa caso o storage ou matchMedia não estejam disponíveis
    }

    this.themeMode.set(initialMode);
    this.applyTheme(initialMode);
  }

  toggleTheme(): void {
    const nextMode: ThemeMode = this.isDark() ? 'light' : 'dark';
    this.setTheme(nextMode);
  }

  setTheme(theme: ThemeMode): void {
    try {
      localStorage.setItem(THEME_STORAGE_KEY, theme);
    } catch {
      // Ignora erro se localStorage estiver bloqueado
    }
    this.themeMode.set(theme);
    this.applyTheme(theme);
  }

  applyTheme(theme: ThemeMode): void {
    if (typeof document === 'undefined') {
      return;
    }

    const root = document.documentElement;
    const body = document.body;

    root.setAttribute('data-theme', theme);

    if (theme === 'dark') {
      body.classList.add('dark-theme');
    } else {
      body.classList.remove('dark-theme');
    }
  }
}
