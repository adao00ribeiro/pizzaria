import { ChangeDetectionStrategy, Component, input } from '@angular/core';

import { TableBlock } from '../../core/models/ai.models';

@Component({
  selector: 'pz-table-block',
  standalone: true,
  templateUrl: './table-block.component.html',
  styleUrl: './table-block.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TableBlockComponent {
  readonly table = input.required<TableBlock>();
}
