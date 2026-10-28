import { computed, inject, Injectable, signal } from '@angular/core';
import { SwUpdate, VersionReadyEvent } from '@angular/service-worker';
import { MatSnackBar } from '@angular/material/snack-bar';
import { filter } from 'rxjs';

export interface BeforeInstallPromptEvent extends Event {
  readonly platforms: string[];
  readonly userChoice: Promise<{
    outcome: 'accepted' | 'dismissed';
    platform: string;
  }>;
  prompt(): Promise<void>;
}

@Injectable({
  providedIn: 'root',
})
export class PwaService {
  private readonly swUpdate = inject(SwUpdate);
  private readonly snackBar = inject(MatSnackBar);

  readonly isOnline = signal<boolean>(
    typeof navigator !== 'undefined' ? navigator.onLine : true,
  );
  readonly promptEvent = signal<BeforeInstallPromptEvent | null>(null);
  readonly canInstall = computed(() => this.promptEvent() !== null);
  readonly hasUpdate = signal<boolean>(false);

  constructor() {
    this.initNetworkListeners();
    this.initUpdateChecker();
    this.initInstallPromptListener();
  }

  private initNetworkListeners(): void {
    if (typeof window === 'undefined') {
      return;
    }

    window.addEventListener('online', () => {
      this.isOnline.set(true);
      this.snackBar.open('Conexão restabelecida!', 'OK', {
        duration: 3500,
        horizontalPosition: 'center',
        verticalPosition: 'bottom',
      });
    });

    window.addEventListener('offline', () => {
      this.isOnline.set(false);
      this.snackBar.open(
        'Você está offline. O POSACHIO continua acessível em modo offline.',
        'OK',
        {
          duration: 6000,
          horizontalPosition: 'center',
          verticalPosition: 'bottom',
        },
      );
    });
  }

  private initUpdateChecker(): void {
    if (!this.swUpdate.isEnabled) {
      return;
    }

    this.swUpdate.versionUpdates
      .pipe(filter((evt): evt is VersionReadyEvent => evt.type === 'VERSION_READY'))
      .subscribe(() => {
        this.hasUpdate.set(true);

        const snackRef = this.snackBar.open(
          'Nova versão disponível!',
          'Atualizar',
          {
            duration: 0, // Permanece visível até o usuário decidir
            horizontalPosition: 'center',
            verticalPosition: 'bottom',
          },
        );

        snackRef.onAction().subscribe(() => {
          this.swUpdate.activateUpdate().then(() => {
            if (typeof document !== 'undefined') {
              document.location.reload();
            }
          });
        });
      });
  }

  private initInstallPromptListener(): void {
    if (typeof window === 'undefined') {
      return;
    }

    window.addEventListener('beforeinstallprompt', (e: Event) => {
      // Previne que o mini-infobar padrão apareça no mobile
      e.preventDefault();
      this.promptEvent.set(e as BeforeInstallPromptEvent);
    });

    window.addEventListener('appinstalled', () => {
      this.promptEvent.set(null);
      this.snackBar.open('POSACHIO instalado com sucesso!', 'Fechar', {
        duration: 4000,
        horizontalPosition: 'center',
        verticalPosition: 'bottom',
      });
    });
  }

  async installApp(): Promise<boolean> {
    const prompt = this.promptEvent();
    if (!prompt) {
      return false;
    }

    try {
      await prompt.prompt();
      const choice = await prompt.userChoice;
      if (choice.outcome === 'accepted') {
        this.promptEvent.set(null);
        return true;
      }
      return false;
    } catch {
      return false;
    }
  }

  async checkForUpdate(): Promise<boolean> {
    if (!this.swUpdate.isEnabled) {
      return false;
    }
    return this.swUpdate.checkForUpdate();
  }
}
