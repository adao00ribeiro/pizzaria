import { ChangeDetectionStrategy, Component, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { FormControl, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTooltipModule } from '@angular/material/tooltip';

import { WhatsAppService } from './whatsapp.service';

@Component({
  selector: 'pz-whatsapp-connect-dialog',
  standalone: true,
  imports: [
    FormsModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatTabsModule,
    MatButtonModule,
    MatIconModule,
    MatFormFieldModule,
    MatInputModule,
    MatChipsModule,
    MatTooltipModule,
    MatSlideToggleModule,
    MatProgressBarModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
  ],
  templateUrl: './whatsapp-connect.dialog.html',
  styleUrl: './whatsapp-connect.dialog.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class WhatsAppConnectDialog implements OnInit, OnDestroy {
  readonly dialogRef = inject(MatDialogRef<WhatsAppConnectDialog>);
  readonly whatsappService = inject(WhatsAppService);
  private readonly snack = inject(MatSnackBar);

  readonly selectedTabIndex = signal<number>(0);
  readonly refreshingQr = signal<boolean>(false);
  readonly loadingQr = signal<boolean>(false);
  readonly showApiToken = signal<boolean>(false);

  // QR Code Real da Evolution API
  readonly realQrCode = this.whatsappService.realQrCode;

  private pollingTimer: ReturnType<typeof setInterval> | null = null;

  // Formulário do número da pizzaria
  readonly phoneControl = new FormControl<string>(this.whatsappService.companyPhone(), {
    nonNullable: true,
    validators: [Validators.required, Validators.minLength(10)],
  });

  // Formulário de teste da Aba 3 (wa.me)
  readonly testPhoneControl = new FormControl<string>('(11) 98765-4321', { nonNullable: true });
  readonly testMessageType = new FormControl<'order' | 'menu' | 'pix' | 'delivery'>('order', {
    nonNullable: true,
  });

  // Signals derivados do serviço
  readonly instanceStatus = this.whatsappService.instanceStatus;
  readonly isConnected = this.whatsappService.isConnected;
  readonly isConnecting = this.whatsappService.isConnecting;
  readonly companyPhone = this.whatsappService.companyPhone;
  readonly webhookUrl = this.whatsappService.webhookUrl;
  readonly apiToken = this.whatsappService.apiToken;
  readonly aiActive = this.whatsappService.aiAutomationActive;

  ngOnInit(): void {
    this.whatsappService.fetchInstanceDetails().subscribe({
      next: () => {
        if (this.whatsappService.isConnected()) {
          this.phoneControl.setValue(this.whatsappService.companyPhone());
          this.snack.open('WhatsApp já está conectado à Evolution API!', 'OK', { duration: 3000 });
        } else {
          this.loadQr();
          this.startPolling();
        }
      },
      error: () => {
        this.whatsappService.checkRealConnectionState().subscribe({
          next: (res) => {
            const state = res.instance?.state ?? res.state;
            if (state === 'open') {
              this.snack.open('WhatsApp já está conectado à Evolution API!', 'OK', { duration: 3000 });
            } else {
              this.loadQr();
              this.startPolling();
            }
          },
          error: () => {
            this.loadQr();
            this.startPolling();
          },
        });
      },
    });
  }

  ngOnDestroy(): void {
    this.stopPolling();
  }

  loadQr(): void {
    if (this.isConnected()) {
      this.snack.open('WhatsApp já está conectado!', 'OK', { duration: 3000 });
      return;
    }
    this.loadingQr.set(true);
    this.whatsappService.fetchRealQrCode().subscribe({
      next: (res) => {
        this.loadingQr.set(false);
        if (res.base64) {
          this.snack.open('QR Code oficial da Evolution API gerado com sucesso!', 'OK', {
            duration: 3000,
          });
        }
      },
      error: (err) => {
        this.loadingQr.set(false);
        console.error('Falha ao carregar QR Code da Evolution API:', err);
      },
    });
  }

  startPolling(): void {
    this.stopPolling();
    this.pollingTimer = setInterval(() => {
      this.whatsappService.checkRealConnectionState().subscribe({
        next: (res) => {
          const state = res.instance?.state ?? res.state;
          if (state === 'open') {
            this.stopPolling();
            this.phoneControl.setValue(this.whatsappService.companyPhone());
            this.snack.open('WhatsApp conectado com sucesso!', 'Excelente', {
              duration: 4000,
            });
          }
        },
        error: () => {},
      });
    }, 3500);
  }

  stopPolling(): void {
    if (this.pollingTimer) {
      clearInterval(this.pollingTimer);
      this.pollingTimer = null;
    }
  }

  saveCompanyPhone(): void {
    if (this.phoneControl.invalid) {
      this.snack.open('Por favor, informe um número de WhatsApp válido com DDD.', 'Atenção', {
        duration: 3000,
      });
      return;
    }

    const phone = this.phoneControl.value.trim();
    this.whatsappService.updateCompanyPhone(phone);
    this.snack.open(`Número oficial atualizado para ${phone} com sucesso!`, 'OK', {
      duration: 3500,
    });
  }

  simulateScan(): void {
    this.whatsappService.connectInstance();
    this.stopPolling();
    this.snack.open('QR Code lido com sucesso! Sincronizando conversas do WhatsApp...', 'OK', {
      duration: 3000,
    });
  }

  disconnect(): void {
    this.whatsappService.disconnectInstance().subscribe({
      next: () => {
        this.snack.open('Instância do WhatsApp desconectada. Leia o QR Code novamente para reconectar.', 'OK', {
          duration: 3500,
        });
        this.loadQr();
        this.startPolling();
      },
      error: () => {
        this.snack.open('Instância desconectada localmente. Gerando novo QR Code...', 'OK', {
          duration: 3500,
        });
        this.loadQr();
        this.startPolling();
      },
    });
  }

  refreshQr(): void {
    this.loadQr();
  }

  copyToClipboard(text: string, label: string): void {
    if (typeof navigator !== 'undefined' && navigator.clipboard) {
      navigator.clipboard.writeText(text).then(() => {
        this.snack.open(`${label} copiado para a área de transferência!`, 'OK', {
          duration: 2500,
        });
      });
    } else {
      this.snack.open(`Não foi possível copiar automaticamente: ${text}`, 'OK', {
        duration: 4000,
      });
    }
  }

  regenerateToken(): void {
    const newToken = this.whatsappService.regenerateToken();
    this.snack.open('Novo token de API gerado com sucesso!', 'OK', {
      duration: 3000,
    });
  }

  toggleAiAutomation(): void {
    this.whatsappService.toggleAiAutomation();
    const active = this.whatsappService.aiAutomationActive();
    this.snack.open(
      active
        ? 'Automação IA ativada para interpretar pedidos e sugerir respostas!'
        : 'Automação IA pausada.',
      'OK',
      { duration: 3000 }
    );
  }

  testWaMeLink(): void {
    const phoneRaw = this.testPhoneControl.value.replace(/\D/g, '');
    if (!phoneRaw) {
      this.snack.open('Informe um telefone para testar o envio.', 'Atenção', { duration: 3000 });
      return;
    }

    const phoneFull = phoneRaw.startsWith('55') ? phoneRaw : `55${phoneRaw}`;
    let message = '';

    switch (this.testMessageType.value) {
      case 'order':
        message = 'Olá! Seu pedido #1042 na Pizzaria Lucrativa foi confirmado e já está no forno! Agradecemos a preferência! 🍕';
        break;
      case 'menu':
        message = 'Olá! Conheça nosso cardápio digital completo com promoções e fotos: https://pizzarialucrativa.app/menu 🍕';
        break;
      case 'pix':
        message = 'Olá! Segue a chave Pix da Pizzaria Lucrativa: (11) 98765-4321. Por favor, envie o comprovante após a transferência!';
        break;
      case 'delivery':
        message = 'Seu pedido saiu para entrega com nosso motoboy! Previsão de chegada em 15 minutos. Bom apetite! 🛵';
        break;
    }

    const encoded = encodeURIComponent(message);
    const url = `https://wa.me/${phoneFull}?text=${encoded}`;
    window.open(url, '_blank');
  }
}
