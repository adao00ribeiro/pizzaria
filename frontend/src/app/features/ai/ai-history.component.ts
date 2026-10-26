import {
  ChangeDetectionStrategy,
  Component,
  inject,
  input,
  output,
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';

import { ConversationSummary } from '../../core/models/ai.models';
import { ConfirmDialog, ConfirmDialogData } from '../../shared/components/confirm-dialog';

@Component({
  selector: 'pz-ai-history',
  standalone: true,
  imports: [
    MatButtonModule,
    MatIconModule,
    MatTooltipModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './ai-history.component.html',
  styleUrl: './ai-history.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AiHistoryComponent {
  private readonly dialog = inject(MatDialog);

  readonly conversations = input.required<ConversationSummary[]>();
  readonly activeId = input<string | null>(null);
  readonly loading = input<boolean>(false);

  readonly selectConversation = output<string>();
  readonly newConversation = output<void>();
  readonly deleteConversation = output<string>();

  formatDate(isoDate: string): string {
    try {
      const d = new Date(isoDate);
      return d.toLocaleDateString('pt-BR', { day: '2-digit', month: '2-digit' });
    } catch {
      return '';
    }
  }

  onDelete(event: Event, item: ConversationSummary): void {
    event.stopPropagation();

    const data: ConfirmDialogData = {
      title: 'Excluir conversa',
      message: `Tem certeza que deseja excluir "${item.title}"? Esta ação removerá o histórico desta conversa.`,
      confirmLabel: 'Excluir',
      cancelLabel: 'Cancelar',
    };

    this.dialog
      .open(ConfirmDialog, { data })
      .afterClosed()
      .subscribe((confirmed) => {
        if (confirmed) {
          this.deleteConversation.emit(item.id);
        }
      });
  }
}
