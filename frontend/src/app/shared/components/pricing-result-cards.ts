import { ChangeDetectionStrategy, Component, input } from '@angular/core';

import { MarginStatus } from '../../core/models/common.models';
import { MarginStatusChip } from './margin-status-chip';
import { BrlPipe } from '../pipes/brl.pipe';
import { PctPipe } from '../pipes/pct.pipe';

/**
 * Cards de resultado de precificação reutilizados em ficha técnica,
 * consulta individual, mistas e combos (Custo · Preço · Lucro · Margem · Status).
 * Todos os inputs são opcionais: cada tela mostra só o que a API devolveu.
 */
@Component({
  selector: 'pz-result-cards',
  standalone: true,
  imports: [MarginStatusChip, BrlPipe, PctPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="result-grid">
      @if (isDefined(directCost())) {
        <div class="result-cell">
          <div class="result-label">Custo direto</div>
          <div class="result-value">{{ directCost() | brl }}</div>
        </div>
      }
      @if (isDefined(indirectCost())) {
        <div class="result-cell">
          <div class="result-label">Custo indireto</div>
          <div class="result-value">{{ indirectCost() | brl }}</div>
        </div>
      }
      @if (isDefined(totalCost())) {
        <div class="result-cell">
          <div class="result-label">Custo total</div>
          <div class="result-value">{{ totalCost() | brl }}</div>
        </div>
      }
      @if (isDefined(price())) {
        <div class="result-cell is-highlight">
          <div class="result-label">{{ priceLabel() }}</div>
          <div class="result-value">{{ price() | brl }}</div>
        </div>
      }
      @if (isDefined(cardPrice())) {
        <div class="result-cell">
          <div class="result-label">Preço no cartão</div>
          <div class="result-value">{{ cardPrice() | brl }}</div>
        </div>
      }
      @if (isDefined(taxValue())) {
        <div class="result-cell">
          <div class="result-label">Taxas</div>
          <div class="result-value">{{ taxValue() | brl }}</div>
        </div>
      }
      @if (isDefined(profit())) {
        <div class="result-cell">
          <div class="result-label">Lucro estimado</div>
          <div class="result-value">{{ profit() | brl }}</div>
        </div>
      }
      @if (isDefined(margin())) {
        <div class="result-cell">
          <div class="result-label">Margem</div>
          <div class="result-value">{{ margin() | pct }}</div>
          @if (isDefined(marginTarget())) {
            <div class="hint-text">alvo {{ marginTarget() | pct }}</div>
          }
        </div>
      }
      @if (status() !== null && status() !== undefined) {
        <div class="result-cell">
          <div class="result-label">Status</div>
          <pz-margin-status-chip [status]="status()!" />
        </div>
      }
    </div>
  `,
})
export class PricingResultCards {
  readonly directCost = input<number | null | undefined>(undefined);
  readonly indirectCost = input<number | null | undefined>(undefined);
  readonly totalCost = input<number | null | undefined>(undefined);
  readonly price = input<number | null | undefined>(undefined);
  readonly priceLabel = input('Preço sugerido');
  readonly cardPrice = input<number | null | undefined>(undefined);
  readonly taxValue = input<number | null | undefined>(undefined);
  readonly profit = input<number | null | undefined>(undefined);
  readonly margin = input<number | null | undefined>(undefined);
  readonly marginTarget = input<number | null | undefined>(undefined);
  readonly status = input<MarginStatus | null | undefined>(undefined);

  isDefined(value: number | null | undefined): boolean {
    return value !== null && value !== undefined && !Number.isNaN(Number(value));
  }
}
