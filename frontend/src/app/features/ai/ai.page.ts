import { CurrencyPipe, DecimalPipe } from '@angular/common';
import {
  AfterViewChecked,
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
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ActivatedRoute } from '@angular/router';
import { Subscription } from 'rxjs';

import {
  AiUsage,
  ConversationDetail,
  ConversationSummary,
} from '../../core/models/ai.models';
import { AiHistoryComponent } from './ai-history.component';
import { AiService } from './ai.service';
import { ChatMessageComponent, UiChatMessage } from './chat-message.component';

@Component({
  selector: 'pz-ai-page',
  standalone: true,
  imports: [
    FormsModule,
    CurrencyPipe,
    DecimalPipe,
    MatButtonModule,
    MatIconModule,
    MatTooltipModule,
    MatProgressSpinnerModule,
    AiHistoryComponent,
    ChatMessageComponent,
  ],
  templateUrl: './ai.page.html',
  styleUrl: './ai.page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AiPage implements OnInit, AfterViewChecked, OnDestroy {
  private readonly aiService = inject(AiService);
  private readonly route = inject(ActivatedRoute);
  private routeSub?: Subscription;

  @ViewChild('messagesViewport') private messagesViewport?: ElementRef<HTMLDivElement>;
  @ViewChild('messageInput') private messageInput?: ElementRef<HTMLTextAreaElement>;

  readonly messages = signal<UiChatMessage[]>([]);
  readonly conversations = signal<ConversationSummary[]>([]);
  readonly currentConversationId = signal<string | null>(null);
  readonly loading = signal<boolean>(false);
  readonly loadingHistory = signal<boolean>(false);
  readonly error = signal<string | null>(null);
  readonly suggestions = signal<string[]>([]);
  readonly usage = signal<AiUsage | null>(null);
  readonly historyOpen = signal<boolean>(false);

  inputText = '';
  private shouldScrollBottom = false;
  private lastUserMessage = '';

  readonly canSend = computed(() => {
    return this.inputText.trim().length > 0;
  });

  ngOnInit(): void {
    this.loadHistory();
    this.loadSuggestions();
    this.loadUsage();

    this.routeSub = this.route.queryParams.subscribe((params) => {
      const q = params['q'];
      if (q && typeof q === 'string' && q.trim().length > 0) {
        this.send(q.trim());
      }
    });
  }

  ngAfterViewChecked(): void {
    if (this.shouldScrollBottom) {
      this.scrollToBottom();
      this.shouldScrollBottom = false;
    }
  }

  ngOnDestroy(): void {
    this.routeSub?.unsubscribe();
  }

  loadHistory(): void {
    this.loadingHistory.set(true);
    this.aiService.listConversations().subscribe({
      next: (list) => {
        this.conversations.set(list);
        this.loadingHistory.set(false);
      },
      error: () => {
        this.loadingHistory.set(false);
      },
    });
  }

  loadSuggestions(): void {
    this.aiService.suggestions().subscribe({
      next: (s) => this.suggestions.set(s),
      error: () => {},
    });
  }

  loadUsage(): void {
    this.aiService.usage().subscribe({
      next: (u) => this.usage.set(u),
      error: () => {},
    });
  }

  loadConversation(id: string): void {
    if (this.loading()) return;
    this.loading.set(true);
    this.error.set(null);
    this.historyOpen.set(false);

    this.aiService.getConversation(id).subscribe({
      next: (detail: ConversationDetail) => {
        this.currentConversationId.set(detail.id);
        const uiMsgs: UiChatMessage[] = detail.messages.map((m) => ({
          id: m.id,
          role: m.role === 'user' ? 'user' : 'model',
          text: m.content,
          createdAt: m.createdAt,
        }));
        this.messages.set(uiMsgs);
        this.loading.set(false);
        this.shouldScrollBottom = true;
      },
      error: () => {
        this.error.set('Não foi possível carregar o histórico desta conversa.');
        this.loading.set(false);
      },
    });
  }

  newConversation(): void {
    if (this.loading()) return;
    this.currentConversationId.set(null);
    this.messages.set([]);
    this.error.set(null);
    this.inputText = '';
    this.historyOpen.set(false);
  }

  deleteConversation(id: string): void {
    this.aiService.deleteConversation(id).subscribe({
      next: () => {
        this.conversations.update((prev) => prev.filter((c) => c.id !== id));
        if (this.currentConversationId() === id) {
          this.newConversation();
        }
      },
      error: () => {
        this.error.set('Erro ao excluir conversa.');
      },
    });
  }

  onKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      this.submit();
    }
  }

  submit(): void {
    const text = this.inputText.trim();
    if (!text || this.loading()) return;
    this.inputText = '';
    this.send(text);
  }

  send(userText: string): void {
    if (!userText || this.loading()) return;

    this.lastUserMessage = userText;
    this.error.set(null);

    // 1. Mensagem otimista do usuário
    const userMsg: UiChatMessage = {
      role: 'user',
      text: userText,
      createdAt: new Date().toISOString(),
    };

    // 2. Placeholder da resposta da IA com loading
    const pendingMsg: UiChatMessage = {
      role: 'model',
      text: '',
      isPending: true,
      createdAt: new Date().toISOString(),
    };

    this.messages.update((prev) => [...prev, userMsg, pendingMsg]);
    this.loading.set(true);
    this.shouldScrollBottom = true;

    const convId = this.currentConversationId();

    this.aiService.chat(userText, convId).subscribe({
      next: (answer) => {
        this.currentConversationId.set(answer.conversationId);
        this.usage.set(answer.usage);

        // Substitui a mensagem pending pela resposta real
        this.messages.update((prev) => {
          const updated = [...prev];
          const lastIdx = updated.length - 1;
          updated[lastIdx] = {
            id: answer.messageId,
            role: 'model',
            text: answer.text,
            blocks: answer.blocks,
            meta: answer.meta,
            usage: answer.usage,
            createdAt: new Date().toISOString(),
          };
          return updated;
        });

        this.loading.set(false);
        this.shouldScrollBottom = true;
        this.loadHistory(); // Atualiza títulos/contadores na barra lateral
      },
      error: (err) => {
        const errorText =
          err?.error?.message ||
          'Desculpe, ocorreu um erro ao consultar os dados. Tente novamente mais tarde.';

        this.messages.update((prev) => {
          const updated = [...prev];
          const lastIdx = updated.length - 1;
          updated[lastIdx] = {
            role: 'model',
            text: '',
            isError: true,
            errorMessage: errorText,
            createdAt: new Date().toISOString(),
          };
          return updated;
        });

        this.loading.set(false);
        this.shouldScrollBottom = true;
      },
    });
  }

  retry(): void {
    if (this.lastUserMessage) {
      // Remove a última mensagem de erro
      this.messages.update((prev) => {
        const filtered = [...prev];
        if (filtered.length > 0 && filtered[filtered.length - 1].isError) {
          filtered.pop();
        }
        if (filtered.length > 0 && filtered[filtered.length - 1].role === 'user') {
          filtered.pop();
        }
        return filtered;
      });

      this.send(this.lastUserMessage);
    }
  }

  private scrollToBottom(): void {
    if (this.messagesViewport) {
      const el = this.messagesViewport.nativeElement;
      el.scrollTop = el.scrollHeight;
    }
  }
}
