import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient, HttpContext, HttpHeaders } from '@angular/common/http';
import { Observable, catchError, forkJoin, map, of, tap } from 'rxjs';
import { SILENT_ERROR } from '../../core/interceptors/http-context';
import { TenantService } from '../../core/services/tenant.service';
import { AiOrderSuggestion, WhatsAppContact, WhatsAppMessage } from './whatsapp.models';

@Injectable({ providedIn: 'root' })
export class WhatsAppService {
  private readonly http = inject(HttpClient);
  private readonly tenant = inject(TenantService);

  private readonly evolutionApiUrl = 'http://localhost:8080';
  private readonly evolutionApiKey = 'pizzaria-lucrativa-secret-key-2026';

  readonly realQrCode = signal<string | null>(null);


  readonly contacts = signal<WhatsAppContact[]>([]);

  readonly companyPhone = signal<string>(
    typeof localStorage !== 'undefined'
      ? localStorage.getItem('pz_whatsapp_company_phone') || ''
      : ''
  );
  readonly instanceStatus = signal<'connected' | 'disconnected' | 'connecting'>('disconnected');
  readonly isConnected = computed(() => this.instanceStatus() === 'connected');
  readonly isConnecting = computed(() => this.instanceStatus() === 'connecting');
  readonly webhookUrl = signal<string>('https://api.pizzarialucrativa.com/v1/whatsapp/webhook');
  readonly apiToken = signal<string>('pz_live_sec_99a8f27b140cd6e88e1');

  constructor() {
    this.fetchInstanceDetails().subscribe({
      error: () => {
        this.checkRealConnectionState().subscribe({
          error: () => {},
        });
      },
    });
  }

  readonly activeContactId = signal<string>('');
  readonly aiAutomationActive = signal<boolean>(true);
  readonly searchQuery = signal<string>('');
  readonly filterMode = signal<'all' | 'orders' | 'unread'>('all');

  readonly activeContact = computed(() => {
    const list = this.contacts();
    const id = this.activeContactId();
    return list.find((c) => c.id === id) ?? list[0] ?? null;
  });

  readonly filteredContacts = computed(() => {
    let list = this.contacts();
    const rawQ = this.searchQuery().trim().toLowerCase();
    const mode = this.filterMode();

    if (rawQ) {
      const qDigits = rawQ.replace(/\D/g, '');
      list = list.filter((c) => {
        const nameMatch = c.name?.toLowerCase().includes(rawQ);
        const textMatch = c.lastMessageText?.toLowerCase().includes(rawQ);
        const phoneMatch = qDigits.length >= 2 && c.phone?.replace(/\D/g, '').includes(qDigits);
        return !!(nameMatch || textMatch || phoneMatch);
      });
    }

    if (mode === 'orders') {
      list = list.filter((c) => c.hasActiveOrder);
    } else if (mode === 'unread') {
      list = list.filter((c) => c.unreadCount > 0);
    }

    return list;
  });

  readonly unreadTotal = computed(() => {
    return this.contacts().reduce((acc, c) => acc + (c.unreadCount > 0 ? 1 : 0), 0);
  });

  readonly activeOrdersCount = computed(() => {
    return this.contacts().filter((c) => c.hasActiveOrder).length;
  });

  selectContact(id: string): void {
    this.activeContactId.set(id);
    this.contacts.update((list) =>
      list.map((c) => (c.id === id && c.unreadCount > 0 ? { ...c, unreadCount: 0 } : c))
    );
    if (id.includes('@')) {
      this.loadMessagesForChat(id).subscribe({ error: () => {} });
    }
  }

  loadRealChats(): Observable<WhatsAppContact[]> {
    const headers = new HttpHeaders({
      apikey: this.evolutionApiKey,
      'Content-Type': 'application/json',
    });
    const context = new HttpContext().set(SILENT_ERROR, true);

    return forkJoin({
      chats: this.http
        .post<any[]>(`${this.evolutionApiUrl}/chat/findChats/pizzaria`, {}, { headers, context })
        .pipe(catchError(() => of([]))),
      contacts: this.http
        .post<any[]>(`${this.evolutionApiUrl}/chat/findContacts/pizzaria`, {}, { headers, context })
        .pipe(catchError(() => of([]))),
    }).pipe(
      map(({ chats, contacts }) => {
        if (!Array.isArray(chats) || chats.length === 0) {
          return this.contacts();
        }

        // Dicionário de contatos por remoteJid para obter nomes reais e fotos
        const contactMap = new Map<string, any>();
        if (Array.isArray(contacts)) {
          contacts.forEach((c) => {
            if (c.remoteJid) {
              contactMap.set(c.remoteJid, c);
            }
          });
        }

        // Filtra chats internos (@lid) ou status de broadcast
        const validChats = chats.filter(
          (c) =>
            c &&
            c.remoteJid &&
            !c.remoteJid.endsWith('@lid') &&
            !c.remoteJid.includes('status@broadcast')
        );

        // Ordena chats por atividade mais recente
        validChats.sort((a, b) => {
          const timeA = this.getChatTimestamp(a);
          const timeB = this.getChatTimestamp(b);
          return timeB - timeA;
        });

        const currentList = this.contacts();
        const currentMap = new Map(currentList.map((c) => [c.id, c]));

        const mappedList: WhatsAppContact[] = validChats.map((chat) => {
          const contact = contactMap.get(chat.remoteJid);
          const existing = currentMap.get(chat.remoteJid);

          const rawName =
            contact?.pushName?.trim() ||
            chat.pushName?.trim() ||
            (chat.remoteJid.includes('@s.whatsapp.net')
              ? this.formatPhoneFromJid(chat.remoteJid)
              : chat.remoteJid);

          const phone = chat.remoteJid.includes('@g.us')
            ? 'Grupo WhatsApp'
            : this.formatPhoneFromJid(chat.remoteJid);

          const profilePicUrl = chat.profilePicUrl || contact?.profilePicUrl || undefined;
          const unreadCount = Number(chat.unreadCount ?? 0);
          const lastTime = this.formatMessageTime(
            chat.lastMessage?.messageTimestamp || chat.updatedAt
          );
          const lastText = this.extractMessageText(chat.lastMessage);

          const aiData = this.analyzeAiSuggestion(lastText, rawName);

          // Preserva mensagens já carregadas caso existam
          const messages: WhatsAppMessage[] =
            existing?.messages && existing.messages.length > 0
              ? existing.messages
              : lastText
              ? [
                  {
                    id: chat.lastMessage?.id || `m-init-${Date.now()}`,
                    sender: chat.lastMessage?.key?.fromMe ? 'agent' : 'customer',
                    text: lastText,
                    timestamp: lastTime,
                    status: chat.lastMessage?.status === 'READ' ? 'read' : 'delivered',
                  },
                ]
              : [];

          return {
            id: chat.remoteJid,
            name: rawName,
            phone,
            profilePicUrl,
            unreadCount,
            lastMessageTime: lastTime || existing?.lastMessageTime || '',
            lastMessageText: lastText || existing?.lastMessageText || 'Sem mensagens recentes',
            hasActiveOrder: existing?.hasActiveOrder ?? false,
            activeOrderCode: existing?.activeOrderCode,
            activeOrderStatus: existing?.activeOrderStatus,
            address: existing?.address,
            messages,
            aiSuggestion: aiData.aiSuggestion,
            aiSuggestedReplies: aiData.aiSuggestedReplies,
          };
        });

        if (mappedList.length > 0) {
          this.contacts.set(mappedList);

          // Se o chat ativo atual for mock ou não estiver na lista, seleciona o primeiro chat real
          const activeId = this.activeContactId();
          const exists = mappedList.some((c) => c.id === activeId);
          if (!exists) {
            this.selectContact(mappedList[0].id);
          }
        }

        return mappedList;
      })
    );
  }

  loadMessagesForChat(remoteJid: string): Observable<WhatsAppMessage[]> {
    if (!remoteJid || !remoteJid.includes('@')) {
      return of([]);
    }

    const headers = new HttpHeaders({
      apikey: this.evolutionApiKey,
      'Content-Type': 'application/json',
    });
    const context = new HttpContext().set(SILENT_ERROR, true);

    return this.http
      .post<any>(
        `${this.evolutionApiUrl}/chat/findMessages/pizzaria`,
        {
          where: {
            key: {
              remoteJid,
            },
          },
        },
        { headers, context }
      )
      .pipe(
        catchError(() => of({ messages: { records: [] } })),
        map((res) => {
          const rawRecords: any[] = res?.messages?.records || (Array.isArray(res) ? res : []);
          if (!Array.isArray(rawRecords)) return [];

          // Ordena do mais antigo para o mais recente (ordem cronológica de cima pra baixo)
          const sorted = [...rawRecords].sort((a, b) => {
            const tA = Number(a.messageTimestamp ?? 0);
            const tB = Number(b.messageTimestamp ?? 0);
            return tA - tB;
          });

          const mapped: WhatsAppMessage[] = sorted.map((record) => {
            const isFromMe = record.key?.fromMe === true;
            const text = this.extractMessageText(record);
            const time = this.formatMessageTime(record.messageTimestamp);
            const isRead =
              record.MessageUpdate?.[0]?.status === 'READ' || record.status === 'READ';

            return {
              id: record.id || record.key?.id || `m-${Date.now()}-${Math.random()}`,
              sender: isFromMe ? 'agent' : 'customer',
              text: text || '(Mídia recebida)',
              timestamp: time,
              status: isRead ? 'read' : 'delivered',
            };
          });

          // Atualiza as mensagens do contato
          this.contacts.update((list) =>
            list.map((c) => {
              if (c.id === remoteJid) {
                const latest = mapped[mapped.length - 1];
                return {
                  ...c,
                  messages: mapped,
                  lastMessageText: latest ? latest.text : c.lastMessageText,
                  lastMessageTime: latest ? latest.timestamp : c.lastMessageTime,
                };
              }
              return c;
            })
          );

          return mapped;
        })
      );
  }

  sendMessage(contactId: string, text: string, sender: 'agent' | 'ai' = 'agent'): void {
    if (!text.trim()) return;

    const time = new Date().toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' });
    const newMessage: WhatsAppMessage = {
      id: `m-${Date.now()}-${Math.random()}`,
      sender,
      text: text.trim(),
      timestamp: time,
      status: 'sent',
    };

    // Feedback instantâneo na interface
    this.contacts.update((list) =>
      list.map((c) => {
        if (c.id === contactId) {
          return {
            ...c,
            lastMessageTime: time,
            lastMessageText: text.trim(),
            messages: [...c.messages, newMessage],
          };
        }
        return c;
      })
    );

    // Dispara envio real pelo WhatsApp na Evolution API
    if (sender === 'agent' || sender === 'ai') {
      const isRealJid = contactId.includes('@') || contactId.replace(/\D/g, '').length >= 8;
      if (isRealJid) {
        const number = contactId.includes('@g.us')
          ? contactId
          : contactId.split('@')[0].replace(/\D/g, '');

        const headers = new HttpHeaders({
          apikey: this.evolutionApiKey,
          'Content-Type': 'application/json',
        });

        this.http
          .post(
            `${this.evolutionApiUrl}/message/sendText/pizzaria`,
            { number, text: text.trim() },
            { headers, context: new HttpContext().set(SILENT_ERROR, true) }
          )
          .subscribe({
            next: () => {
              this.contacts.update((list) =>
                list.map((c) => {
                  if (c.id === contactId) {
                    const updatedMessages = c.messages.map((m) =>
                      m.id === newMessage.id ? { ...m, status: 'delivered' as const } : m
                    );
                    return { ...c, messages: updatedMessages };
                  }
                  return c;
                })
              );
            },
            error: (err) => {
              console.error('Erro ao enviar mensagem pelo WhatsApp:', err);
            },
          });
      }
    }
  }

  private getChatTimestamp(chat: any): number {
    if (chat.lastMessage?.messageTimestamp) {
      const ts = Number(chat.lastMessage.messageTimestamp);
      return ts < 10000000000 ? ts * 1000 : ts;
    }
    if (chat.updatedAt) {
      const d = new Date(chat.updatedAt).getTime();
      if (!isNaN(d)) return d;
    }
    return 0;
  }

  extractMessageText(msg: any): string {
    if (!msg) return '';
    const message = msg.message || msg;
    if (typeof message === 'string') return message;
    if (message.conversation) return message.conversation;
    if (message.extendedTextMessage?.text) return message.extendedTextMessage.text;
    if (message.imageMessage) {
      return message.imageMessage.caption ? `📷 ${message.imageMessage.caption}` : '📷 Foto';
    }
    if (message.audioMessage) return '🎵 Áudio';
    if (message.videoMessage) {
      return message.videoMessage.caption ? `📹 ${message.videoMessage.caption}` : '📹 Vídeo';
    }
    if (message.documentMessage) {
      return message.documentMessage.fileName ? `📄 ${message.documentMessage.fileName}` : '📄 Documento';
    }
    if (message.stickerMessage) return '🏷️ Figurinha';
    if (message.contactMessage) return '👤 Contato';
    if (message.locationMessage) return '📍 Localização';
    if (msg.messageType && msg.messageType !== 'conversation') {
      return msg.messageType;
    }
    return '';
  }

  formatMessageTime(ts?: number | string | Date): string {
    if (!ts) return '';
    let d: Date;
    if (typeof ts === 'number') {
      d = new Date(ts < 10000000000 ? ts * 1000 : ts);
    } else {
      d = new Date(ts);
    }
    if (isNaN(d.getTime())) return '';
    const now = new Date();
    const isToday = d.toDateString() === now.toDateString();
    if (isToday) {
      return d.toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' });
    }
    return d.toLocaleDateString('pt-BR', { day: '2-digit', month: '2-digit' });
  }

  private analyzeAiSuggestion(
    lastText: string,
    contactName: string
  ): { aiSuggestion?: AiOrderSuggestion; aiSuggestedReplies?: string[] } {
    if (!lastText) {
      return {
        aiSuggestion: undefined,
        aiSuggestedReplies: [
          `Olá ${contactName}! Como posso te ajudar hoje na Pizzaria Lucrativa?`,
          `Boa noite ${contactName}! Gostaria de conferir nossas pizzas artesanais hoje?`,
        ],
      };
    }

    const lower = lastText.toLowerCase();
    const isOrder = /pizza|entrega|pedido|coca|guaran[aá]|calabresa|portuguesa|frango|mussarela|broto|fatia|pedir|quero/i.test(
      lower
    );
    const isStatus = /status|previs[aã]o|onde est[aá]|demora|saiu|chegando/i.test(lower);
    const isPix = /pix|comprovante|paguei|transferi/i.test(lower);
    const isMenu = /card[aá]pio|sabores|pre[çc]os|promo[çc][aã]o/i.test(lower);

    if (isOrder) {
      return {
        aiSuggestion: {
          detected: true,
          summary: `Pedido detectado via mensagem de ${contactName}`,
          itemsDescription: lastText,
          suggestedType: /entrega|entregar|delivery|rua|av|apto/i.test(lower) ? 'Delivery' : 'Pickup',
          deliveryAddress: /rua|av\.|avenida|alameda|apto|número|bairro/i.test(lower)
            ? lastText
            : undefined,
          estimatedTotal: 65.0,
          confidence: 94,
          extractedNotes: `Pedido WhatsApp: ${lastText}`,
        },
        aiSuggestedReplies: [
          `Perfeito, ${contactName}! Recebemos sua mensagem: "${lastText}". Gostaria de confirmar seu pedido?`,
          `Olá ${contactName}! Já separei sua solicitação. Posso confirmar o endereço de entrega e a forma de pagamento?`,
        ],
      };
    }

    if (isStatus) {
      return {
        aiSuggestion: {
          detected: false,
          summary: `Consulta de status do pedido de ${contactName}`,
          itemsDescription: '',
          suggestedType: 'Delivery',
          confidence: 90,
        },
        aiSuggestedReplies: [
          `Olá ${contactName}! Seu pedido está na esteira de produção e deve sair para entrega em breve!`,
          `Oi ${contactName}! Já verifiquei com o forno: sua pizza está assando quentinha agora!`,
        ],
      };
    }

    if (isPix) {
      return {
        aiSuggestion: {
          detected: false,
          summary: `Comprovante / Pagamento informado`,
          itemsDescription: '',
          suggestedType: 'Delivery',
          confidence: 92,
        },
        aiSuggestedReplies: [
          `Pagamento identificado com sucesso, ${contactName}! Muito obrigado, seu pedido foi liberado.`,
          `Obrigado pelo envio! Já conferimos seu Pix e o pedido está em andamento.`,
        ],
      };
    }

    if (isMenu) {
      return {
        aiSuggestion: {
          detected: false,
          summary: `Solicitação de cardápio / promoções`,
          itemsDescription: '',
          suggestedType: 'Delivery',
          confidence: 91,
        },
        aiSuggestedReplies: [
          `Olá ${contactName}! Aqui está o link do nosso cardápio digital interativo com todos os sabores e preços atualizados!`,
          `Hoje estamos com combo especial de Pizza Grande + Refrigerante 2L com desconto! Acesse nosso cardápio para pedir:`,
        ],
      };
    }

    return {
      aiSuggestion: undefined,
      aiSuggestedReplies: [
        `Olá ${contactName}! Como posso te ajudar hoje na Pizzaria Lucrativa?`,
        `Boa noite ${contactName}! Gostaria de fazer um pedido ou tirar dúvidas sobre o cardápio?`,
      ],
    };
  }


  sendQuickReply(contactId: string, type: 'menu' | 'pix' | 'status'): void {
    const contact = this.contacts().find((c) => c.id === contactId);
    if (!contact) return;

    const origin = typeof window !== 'undefined' ? window.location.origin : 'https://pizzarialucrativa.app';
    const slug = this.tenant.slug() || 'pizzaria';
    const menuUrl = `${origin}/menu/${slug}`;

    let messageText = '';

    switch (type) {
      case 'menu':
        messageText = `🍕 Cardápio Digital Online:\n${menuUrl}\n\nAcesse para escolher seus sabores favoritos, ver fotos reais e personalizar sua pizza!`;
        break;
      case 'pix':
        messageText = `💰 Chave Pix da Pizzaria:\nChave CNPJ / Celular: (11) 98765-4321\nFavorecido: Pizzaria Lucrativa\n\nPor favor, envie o comprovante por aqui assim que efetuar o pagamento para liberarmos seu pedido!`;
        break;
      case 'status':
        if (contact.hasActiveOrder && contact.activeOrderCode) {
          messageText = `🛵 Status do seu Pedido #${contact.activeOrderCode}:\nSituação atual: *${contact.activeOrderStatus ?? 'Em Produção'}*\nPrevisão média: 25 a 40 minutos. Qualquer dúvida estamos à disposição!`;
        } else {
          messageText = `Olá ${contact.name}! No momento não consta nenhum pedido em andamento no seu número. Gostaria de fazer um novo pedido agora?`;
        }
        break;
    }

    this.sendMessage(contactId, messageText, 'agent');
  }

  toggleAiAutomation(): void {
    this.aiAutomationActive.update((v) => !v);
  }

  setSearchQuery(query: string): void {
    this.searchQuery.set(query);
  }

  setFilterMode(mode: 'all' | 'orders' | 'unread'): void {
    this.filterMode.set(mode);
  }

  updateCompanyPhone(phone: string): void {
    const trimmed = phone.trim();
    this.companyPhone.set(trimmed);
    if (typeof localStorage !== 'undefined') {
      try {
        localStorage.setItem('pz_whatsapp_company_phone', trimmed);
      } catch {}
    }
  }

  setInstanceStatus(status: 'connected' | 'disconnected' | 'connecting'): void {
    this.instanceStatus.set(status);
  }

  connectInstance(): void {
    this.instanceStatus.set('connecting');
    setTimeout(() => {
      this.instanceStatus.set('connected');
    }, 1200);
  }

  disconnectInstance(): Observable<any> {
    const headers = new HttpHeaders({
      apikey: this.evolutionApiKey,
    });

    this.instanceStatus.set('disconnected');
    this.realQrCode.set(null);
    this.contacts.set([]);
    this.activeContactId.set('');

    return this.http
      .delete(`${this.evolutionApiUrl}/instance/logout/pizzaria`, {
        headers,
        context: new HttpContext().set(SILENT_ERROR, true),
      })
      .pipe(
        tap(() => {
          this.instanceStatus.set('disconnected');
          this.realQrCode.set(null);
          this.contacts.set([]);
          this.activeContactId.set('');
        }),
        catchError(() => {
          this.instanceStatus.set('disconnected');
          this.realQrCode.set(null);
          this.contacts.set([]);
          this.activeContactId.set('');
          return of(null);
        })
      );
  }

  fetchInstanceDetails(): Observable<any> {
    const headers = new HttpHeaders({
      apikey: this.evolutionApiKey,
    });

    return this.http
      .get<any[]>(`${this.evolutionApiUrl}/instance/fetchInstances`, {
        headers,
        context: new HttpContext().set(SILENT_ERROR, true),
      })
      .pipe(
        tap((instances) => {
          if (Array.isArray(instances)) {
            const pizzaria = instances.find(
              (i: any) => i.name === 'pizzaria' || i.instanceName === 'pizzaria' || i.id === 'pizzaria'
            );
            if (pizzaria && (pizzaria.connectionStatus === 'open' || pizzaria.state === 'open')) {
              this.instanceStatus.set('connected');
              this.loadRealChats().subscribe({ error: () => {} });
              if (pizzaria.ownerJid) {
                const formatted = this.formatPhoneFromJid(pizzaria.ownerJid);
                if (formatted) {
                  this.companyPhone.set(formatted);
                  if (typeof localStorage !== 'undefined') {
                    try {
                      localStorage.setItem('pz_whatsapp_company_phone', formatted);
                    } catch {}
                  }
                }
              }
            } else if (
              pizzaria &&
              (pizzaria.connectionStatus === 'connecting' || pizzaria.state === 'connecting')
            ) {
              this.instanceStatus.set('connecting');
            } else if (
              pizzaria &&
              (pizzaria.connectionStatus === 'close' || pizzaria.state === 'close')
            ) {
              this.instanceStatus.set('disconnected');
            }
          }
        })
      );
  }

  formatPhoneFromJid(jid: string): string {
    const digits = jid.split('@')[0].replace(/\D/g, '');
    const local = digits.startsWith('55') ? digits.slice(2) : digits;
    if (local.length === 11) {
      return `(${local.slice(0, 2)}) ${local.slice(2, 7)}-${local.slice(7)}`;
    }
    if (local.length === 10) {
      if (local[2] === '9') {
        return `(${local.slice(0, 2)}) 9${local.slice(2, 6)}-${local.slice(6)}`;
      }
      return `(${local.slice(0, 2)}) ${local.slice(2, 6)}-${local.slice(6)}`;
    }
    return local || jid;
  }

  fetchRealQrCode(): Observable<{ base64?: string; code?: string; pairingCode?: string }> {
    const headers = new HttpHeaders({
      apikey: this.evolutionApiKey,
    });

    return this.http
      .get<{ base64?: string; code?: string; pairingCode?: string }>(
        `${this.evolutionApiUrl}/instance/connect/pizzaria`,
        { headers }
      )
      .pipe(
        tap((res) => {
          if (res.base64) {
            const qr = res.base64.startsWith('data:')
              ? res.base64
              : `data:image/png;base64,${res.base64}`;
            this.realQrCode.set(qr);
          } else {
            this.realQrCode.set(null);
          }
        })
      );
  }

  checkRealConnectionState(): Observable<{ instance?: { state: string; instanceName?: string }; state?: string }> {
    const headers = new HttpHeaders({
      apikey: this.evolutionApiKey,
    });

    return this.http
      .get<{ instance?: { state: string; instanceName?: string }; state?: string }>(
        `${this.evolutionApiUrl}/instance/connectionState/pizzaria`,
        { headers, context: new HttpContext().set(SILENT_ERROR, true) }
      )
      .pipe(
        tap((res) => {
          const state = res.instance?.state ?? res.state;
          if (state === 'open') {
            this.instanceStatus.set('connected');
            this.fetchInstanceDetails().subscribe({ error: () => {} });
          } else if (state === 'connecting') {
            this.instanceStatus.set('connecting');
          } else if (state === 'close') {
            this.instanceStatus.set('disconnected');
            this.contacts.set([]);
          }
        }),
        catchError(() => {
          this.instanceStatus.set('disconnected');
          this.contacts.set([]);
          return of({ state: 'close' });
        })
      );
  }

  regenerateToken(): string {
    const chars = '0123456789abcdef';
    let rand = '';
    for (let i = 0; i < 20; i++) {
      rand += chars[Math.floor(Math.random() * chars.length)];
    }
    const newToken = `pz_live_sec_${rand}`;
    this.apiToken.set(newToken);
    return newToken;
  }
}
