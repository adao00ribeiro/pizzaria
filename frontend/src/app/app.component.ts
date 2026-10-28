import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { PwaService } from './core/services/pwa.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet],
  template: `<router-outlet />`,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppComponent {
  // Inicializa os listeners reativos de PWA (SW updates, status de rede e instalação A2HS)
  protected readonly pwa = inject(PwaService);
}
