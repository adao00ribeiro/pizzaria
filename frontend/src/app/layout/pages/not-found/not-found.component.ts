import { ChangeDetectionStrategy, Component } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';

/** Página 404 (rota inexistente fora do shell). */
@Component({
  selector: 'pz-not-found',
  standalone: true,
  imports: [MatIconModule, MatButtonModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="not-found-page">
      <mat-icon>search_off</mat-icon>
      <h1>Página não encontrada</h1>
      <p>O endereço acessado não existe ou foi movido.</p>
      <a mat-flat-button color="primary" routerLink="/dashboard">
        <mat-icon>arrow_back</mat-icon>
        Voltar ao dashboard
      </a>
    </div>
  `,
})
export class NotFoundComponent {}
