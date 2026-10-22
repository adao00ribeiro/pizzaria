import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  OnDestroy,
  OnInit,
  ViewChild,
  computed,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormsModule, ReactiveFormsModule } from '@angular/forms';
import { MatBadgeModule } from '@angular/material/badge';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Subscription, debounceTime, interval } from 'rxjs';

import { BrlPipe } from '../../shared/pipes/brl.pipe';
import { NewOrderDialog, NewOrderDialogData } from '../orders/new-order.dialog';
import { WhatsAppConnectDialog } from './whatsapp-connect.dialog';
import { WhatsAppContact } from './whatsapp.models';
import { WhatsAppService } from './whatsapp.service';

@Component({
  selector: 'pz-whatsapp-page',
  standalone: true,
  imports: [
    FormsModule,
    ReactiveFormsModule,
    MatButtonModule,
    MatIconModule,
    MatInputModule,
    MatFormFieldModule,
    MatBadgeModule,
    MatChipsModule,
    MatTooltipModule,
    MatSlideToggleModule,
    MatDialogModule,
    MatSnackBarModule,
    BrlPipe,
  ],
  templateUrl: './whatsapp.page.html',
  styleUrl: './whatsapp.page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class WhatsAppPage implements OnInit, OnDestroy {
  readonly whatsappService = inject(WhatsAppService);
  private readonly dialog = inject(MatDialog);
  private readonly snack = inject(MatSnackBar);
  private pollSub?: Subscription;

  readonly loadingRealChats = signal<boolean>(false);

  @ViewChild('chatScroll') private chatScrollContainer?: ElementRef<HTMLDivElement>;

  readonly messageInput = new FormControl<string>('', { nonNullable: true });
  readonly searchInput = new FormControl<string>('', { nonNullable: true });

  readonly activeContact = this.whatsappService.activeContact;
  readonly contacts = this.whatsappService.filteredContacts;
  readonly filterMode = this.whatsappService.filterMode;
  readonly aiActive = this.whatsappService.aiAutomationActive;
  readonly unreadCount = this.whatsappService.unreadTotal;
  readonly companyPhone = this.whatsappService.companyPhone;
  readonly isConnected = this.whatsappService.isConnected;
  readonly isConnecting = this.whatsappService.isConnecting;

  readonly activeContactInitials = computed(() => {
    const contact = this.activeContact();
    if (!contact) return 'CL';
    return contact.name
      .split(' ')
      .slice(0, 2)
      .map((n) => n[0])
      .join('')
      .toUpperCase();
  });

  constructor() {
    this.searchInput.valueChanges
      .pipe(debounceTime(150), takeUntilDestroyed())
      .subscribe((val) => this.whatsappService.setSearchQuery(val ?? ''));
  }

  ngOnInit(): void {
    // Verifica o status real da conexão logo no carregamento da tela
    this.whatsappService.checkRealConnectionState().subscribe({
      next: () => {
        if (this.whatsappService.isConnected()) {
          this.whatsappService.loadRealChats().subscribe({
            next: () => {
              const active = this.activeContact();
              if (active && active.id.includes('@')) {
                this.whatsappService.loadMessagesForChat(active.id).subscribe();
              }
            },
          });
        }
      },
      error: () => {},
    });

    // Polling a cada 5 segundos para sincronizar novas mensagens automaticamente quando conectado
    this.pollSub = interval(5000).subscribe(() => {
      if (this.whatsappService.isConnected()) {
        this.whatsappService.loadRealChats().subscribe({
          next: () => {
            const active = this.activeContact();
            if (active && active.id.includes('@')) {
              this.whatsappService.loadMessagesForChat(active.id).subscribe();
            }
          },
        });
      }
    });
  }

  ngOnDestroy(): void {
    this.pollSub?.unsubscribe();
  }

  refreshConversations(): void {
    this.loadingRealChats.set(true);
    this.whatsappService.loadRealChats().subscribe({
      next: () => {
        const active = this.activeContact();
        if (active && active.id.includes('@')) {
          this.whatsappService.loadMessagesForChat(active.id).subscribe();
        }
        this.loadingRealChats.set(false);
        this.snack.open('Conversas atualizadas em tempo real!', 'OK', { duration: 2000 });
      },
      error: () => {
        this.loadingRealChats.set(false);
      },
    });
  }

  onSearchChange(): void {
    this.whatsappService.setSearchQuery(this.searchInput.value);
  }

  setFilter(mode: 'all' | 'orders' | 'unread'): void {
    this.whatsappService.setFilterMode(mode);
  }

  selectContact(contact: WhatsAppContact): void {
    this.whatsappService.selectContact(contact.id);
    this.whatsappService.loadMessagesForChat(contact.id).subscribe();
    this.scrollToBottom();
  }

  sendCurrentMessage(): void {
    const text = this.messageInput.value.trim();
    const contact = this.activeContact();
    if (!text || !contact) return;

    this.whatsappService.sendMessage(contact.id, text, 'agent');
    this.messageInput.setValue('');
    this.scrollToBottom();
  }

  sendQuick(type: 'menu' | 'pix' | 'status'): void {
    const contact = this.activeContact();
    if (!contact) return;
    this.whatsappService.sendQuickReply(contact.id, type);
    this.scrollToBottom();
  }

  sendAiSuggestion(reply: string): void {
    const contact = this.activeContact();
    if (!contact) return;
    this.whatsappService.sendMessage(contact.id, reply, 'agent');
    this.scrollToBottom();
  }

  toggleAi(): void {
    this.whatsappService.toggleAiAutomation();
    const active = this.whatsappService.aiAutomationActive();
    this.snack.open(
      active ? 'Automação IA do WhatsApp ativada!' : 'Automação IA do WhatsApp pausada temporariamente.',
      'OK',
      { duration: 2500 }
    );
  }

  openExternalWhatsApp(): void {
    const contact = this.activeContact();
    if (!contact) return;
    const digits = contact.phone.replace(/\D/g, '');
    window.open(`https://wa.me/55${digits}`, '_blank');
  }

  openReconnectModal(): void {
    this.dialog.open(WhatsAppConnectDialog, {
      width: '900px',
      maxWidth: '96vw',
      maxHeight: '92vh',
      panelClass: 'pz-whatsapp-connect-dialog-panel',
    });
  }

  openNewOrderModal(fromAi = false): void {
    const contact = this.activeContact();
    if (!contact) return;

    const initialData: NewOrderDialogData = {
      customerName: contact.name,
      customerPhone: contact.phone,
      deliveryAddress: contact.address || contact.aiSuggestion?.deliveryAddress || '',
      notes:
        fromAi && contact.aiSuggestion?.extractedNotes
          ? contact.aiSuggestion.extractedNotes
          : fromAi && contact.aiSuggestion?.itemsDescription
          ? `Pedido extraído pela IA: ${contact.aiSuggestion.itemsDescription}`
          : '',
      type:
        fromAi && contact.aiSuggestion?.suggestedType
          ? contact.aiSuggestion.suggestedType
          : contact.address
          ? 'Delivery'
          : 'Counter',
    };

    const dialogRef = this.dialog.open(NewOrderDialog, {
      width: '1000px',
      maxWidth: '96vw',
      maxHeight: '94vh',
      data: initialData,
    });

    dialogRef.afterClosed().subscribe((result) => {
      if (result) {
        this.snack.open(`Pedido #${result.code ?? 'gerado'} confirmado com sucesso!`, 'Ok', {
          duration: 4000,
        });

        // Envia mensagem no WhatsApp confirmando para o cliente
        const trackingCode = result.code ? `#${result.code}` : '';
        this.whatsappService.sendMessage(
          contact.id,
          `🍕 Seu pedido ${trackingCode} foi registrado e já está na esteira de produção! Obrigado pela preferência!`
        );
        this.scrollToBottom();
      }
    });
  }

  private scrollToBottom(): void {
    setTimeout(() => {
      if (this.chatScrollContainer) {
        this.chatScrollContainer.nativeElement.scrollTop =
          this.chatScrollContainer.nativeElement.scrollHeight;
      }
    }, 80);
  }
}
