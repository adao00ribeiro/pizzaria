import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { MatChipsModule } from '@angular/material/chips';
import { MatIconModule } from '@angular/material/icon';

import { MarginStatus } from '../../core/models/common.models';
import { MARGIN_STATUS_LABELS } from '../../core/models/labels';

/**
 * Chip de status de margem usado em todas as listas de precificação (NF-U05):
 * verde "Dentro da margem" / vermelho "Precisa ajustar".
 */
@Component({
  selector: 'pz-margin-status-chip',
  standalone: true,
  imports: [MatChipsModule, MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (status() === 'Within') {
      <mat-chip class="pz-chip-ok" title="Dentro da margem">
        <mat-icon matChipAvatar>check_circle</mat-icon>
        Dentro da margem
      </mat-chip>
    } @else {
      <mat-chip class="pz-chip-warn" title="Precisa ajustar">
        <mat-icon matChipAvatar>error</mat-icon>
        Precisa ajustar
      </mat-chip>
    }
  `,
  styles: [
    `
      :host {
        display: inline-block;
      }
      .mat-mdc-chip.pz-chip-ok {
        --mdc-chip-elevated-container-color: #e6f4ea;
        --mdc-chip-elevation-layer-color: #e6f4ea;
        --mdc-chip-label-text-color: #1b5e20;
        --mdc-chip-outline-color: #a5d6a7;
        --mdc-chip-with-avatar-label-text-color: #1b5e20;
        font-size: 0.75rem;
        min-height: 28px;
        .mat-icon {
          color: #1b5e20;
        }
      }
      .mat-mdc-chip.pz-chip-warn {
        --mdc-chip-elevated-container-color: #fdecea;
        --mdc-chip-elevation-layer-color: #fdecea;
        --mdc-chip-label-text-color: #b71c1c;
        --mdc-chip-outline-color: #ef9a9a;
        --mdc-chip-with-avatar-label-text-color: #b71c1c;
        font-size: 0.75rem;
        min-height: 28px;
        .mat-icon {
          color: #b71c1c;
        }
      }
      .mat-icon {
        font-size: 16px !important;
        width: 16px !important;
        height: 16px !important;
      }
      :host-context(.dark-theme) {
        .mat-mdc-chip.pz-chip-ok {
          --mdc-chip-elevated-container-color: rgba(52, 211, 153, 0.15) !important;
          --mdc-chip-elevation-layer-color: rgba(52, 211, 153, 0.15) !important;
          --mdc-chip-label-text-color: #34d399 !important;
          --mdc-chip-with-avatar-label-text-color: #34d399 !important;
          --mdc-chip-outline-color: rgba(52, 211, 153, 0.35) !important;
          color: #34d399 !important;
          .mat-icon {
            color: #34d399 !important;
          }
        }
        .mat-mdc-chip.pz-chip-warn {
          --mdc-chip-elevated-container-color: rgba(248, 113, 113, 0.15) !important;
          --mdc-chip-elevation-layer-color: rgba(248, 113, 113, 0.15) !important;
          --mdc-chip-label-text-color: #f87171 !important;
          --mdc-chip-with-avatar-label-text-color: #f87171 !important;
          --mdc-chip-outline-color: rgba(248, 113, 113, 0.35) !important;
          color: #f87171 !important;
          .mat-icon {
            color: #f87171 !important;
          }
        }
      }
    `,
  ],
})
export class MarginStatusChip {
  readonly status = input.required<MarginStatus>();

  readonly labels = MARGIN_STATUS_LABELS;
}
