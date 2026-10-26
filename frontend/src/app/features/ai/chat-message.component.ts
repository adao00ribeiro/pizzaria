import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';

import {
  AnswerBlock,
  AiMeta,
  AiUsage,
} from '../../core/models/ai.models';
import { AiService } from './ai.service';
import { ChartBlockComponent } from './chart-block.component';
import { KpiBlockComponent } from './kpi-block.component';
import { TableBlockComponent } from './table-block.component';

export interface UiChatMessage {
  id?: string;
  role: 'user' | 'model';
  text: string;
  blocks?: AnswerBlock[];
  meta?: AiMeta;
  usage?: AiUsage;
  createdAt?: string;
  isPending?: boolean;
  isError?: boolean;
  errorMessage?: string;
}

interface TextSegment {
  type: 'paragraph' | 'bullet';
  spans: { text: string; bold: boolean }[];
}

@Component({
  selector: 'pz-chat-message',
  standalone: true,
  imports: [
    MatButtonModule,
    MatIconModule,
    MatTooltipModule,
    ChartBlockComponent,
    TableBlockComponent,
    KpiBlockComponent,
  ],
  templateUrl: './chat-message.component.html',
  styleUrl: './chat-message.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ChatMessageComponent {
  private readonly aiService = inject(AiService);

  readonly message = input.required<UiChatMessage>();
  readonly retry = output<void>();

  readonly showMeta = signal(false);
  readonly copied = signal(false);
  readonly feedbackGiven = signal<'up' | 'down' | null>(null);
  readonly feedbackSent = signal(false);

  readonly textSegments = computed<TextSegment[]>(() => {
    const raw = this.message().text;
    if (!raw) return [];

    const lines = raw.split(/\r?\n/);
    const segments: TextSegment[] = [];

    for (const line of lines) {
      const trimmed = line.trim();
      if (!trimmed) continue;

      const isBullet = trimmed.startsWith('- ') || trimmed.startsWith('* ');
      const content = isBullet ? trimmed.slice(2) : trimmed;

      const spans: { text: string; bold: boolean }[] = [];
      const parts = content.split(/(\*\*[^*]+\*\*)/g);

      for (const part of parts) {
        if (!part) continue;
        if (part.startsWith('**') && part.endsWith('**') && part.length >= 4) {
          spans.push({ text: part.slice(2, -2), bold: true });
        } else {
          spans.push({ text: part, bold: false });
        }
      }

      segments.push({
        type: isBullet ? 'bullet' : 'paragraph',
        spans,
      });
    }

    return segments;
  });

  formatTime(isoDate: string): string {
    try {
      const date = new Date(isoDate);
      return date.toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' });
    } catch {
      return '';
    }
  }

  async copyText(): Promise<void> {
    const msg = this.message();
    if (!msg.text) return;

    try {
      await navigator.clipboard.writeText(msg.text);
      this.copied.set(true);
      setTimeout(() => this.copied.set(false), 2000);
    } catch {
      // Falha silenciosa em navegadores restritivos
    }
  }

  giveFeedback(helpful: boolean): void {
    if (this.feedbackSent()) return;

    const msg = this.message();
    this.feedbackGiven.set(helpful ? 'up' : 'down');
    this.feedbackSent.set(true);

    this.aiService
      .feedback({
        messageId: msg.id || null,
        helpful,
      })
      .subscribe({
        error: () => {
          // Não bloqueia chat em caso de erro no feedback
        },
      });
  }
}
