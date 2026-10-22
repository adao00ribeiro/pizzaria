# 08 — Frontend Angular 18 + Angular Material

## 1. Estrutura de pastas

```
frontend/
├── src/app/
│   ├── core/                          # singletones
│   │   ├── guards/                    # auth.guard, tenant.guard
│   │   ├── interceptors/              # jwt.interceptor, error.interceptor
│   │   ├── services/                  # auth.service, storage.service
│   │   └── models/                    # interfaces compartilhadas
│   │
│   ├── shared/                        # reutilizáveis
│   │   ├── components/                # margin-status-chip, money-input, confirm-dialog
│   │   └── pipes/                     # brl-currency, percent
│   │
│   ├── layout/
│   │   ├── shell/                     # mat-sidenav + toolbar + menu
│   │   └── pages/                     # login, register, not-found
│   │
│   └── features/
│       ├── dashboard/
│       ├── orders/                    # tablet do atendente (/pedidos + /pedidos/:id/ticket)
│       ├── kitchen/                   # fila da produção (kanban do pizzaiolo)
│       ├── public/                    # cardápio público, checkout e acompanhamento sem login
│       ├── profit/                    # relatório de lucro real (/lucro — owner)
│       ├── team/                      # gestão de equipe e papéis (owner)
│       ├── ingredients/
│       ├── preparations/
│       ├── fixed-costs/
│       ├── fees/                      # tela única de configuração de taxas + slug do cardápio
│       ├── products/                  # produtos + tamanhos
│       ├── technical-sheets/          # montador de ficha + precificação
│       ├── pricing/                   # mistas, combos, bebidas
│       ├── stock/
│       └── alerts/
│
├── src/environments/                  # environment.ts / environment.prod.ts
└── angular.json
```

## 2. Módulos e rotas

Rotas lazy-loaded por feature (standalone components, sem NgModules):

```typescript
const routes: Routes = [
  { path: 'login',    component: LoginComponent },
  { path: 'register', component: RegisterComponent },
  // Rotas públicas (fora do Shell, sem authGuard — Sprint 5)
  { path: 'menu/:slug',              loadComponent: () => import('./features/public/public-menu.page') },
  { path: 'checkout/:slug',          loadComponent: () => import('./features/public/public-checkout.page') },
  { path: 'acompanhar/:slug/:code',  loadComponent: () => import('./features/public/public-tracking.page') },
  // Rotas autenticadas (dentro do Shell)
  { path: '',         component: ShellComponent, canActivate: [authGuard], children: [
    { path: 'dashboard',           canActivate: [roleGuard('owner'), tenantGuard], loadComponent: () => import('./features/dashboard/dashboard.page') },
    { path: 'pedidos',             canActivate: [roleGuard('owner', 'atendente')], loadComponent: () => import('./features/orders/orders.page') },
    { path: 'pedidos/:id/ticket',  canActivate: [roleGuard('owner', 'atendente')], loadComponent: () => import('./features/orders/order-ticket.page') },
    { path: 'producao',            canActivate: [roleGuard('owner', 'pizzaiolo')], loadComponent: () => import('./features/kitchen/kitchen.page') },
    { path: 'lucro',               canActivate: [roleGuard('owner')], loadComponent: () => import('./features/profit/profit.page') },
    { path: 'team',                canActivate: [roleGuard('owner')], loadComponent: () => import('./features/team/team.page') },
    { path: 'ingredients',         canActivate: [roleGuard('owner')], loadComponent: () => import('./features/ingredients/ingredients.page') },
    { path: 'preparations',        canActivate: [roleGuard('owner')], loadComponent: () => import('./features/preparations/preparations.page') },
    { path: 'fixed-costs',         canActivate: [roleGuard('owner')], loadComponent: () => import('./features/fixed-costs/fixed-costs.page') },
    { path: 'fees',                canActivate: [roleGuard('owner')], loadComponent: () => import('./features/fees/fees.page') },
    { path: 'products',            canActivate: [roleGuard('owner')], loadComponent: () => import('./features/products/products.page') },
    { path: 'sheets',              canActivate: [roleGuard('owner')], loadComponent: () => import('./features/technical-sheets/sheets.page') },
    { path: 'sheets/:id/edit',     canActivate: [roleGuard('owner')], loadComponent: () => import('./features/technical-sheets/sheet-editor.page') },
    { path: 'pricing/half-half',   canActivate: [roleGuard('owner')], loadComponent: () => import('./features/pricing/half-half.page') },
    { path: 'pricing/combos',      canActivate: [roleGuard('owner')], loadComponent: () => import('./features/pricing/combos.page') },
    { path: 'pricing/beverages',   canActivate: [roleGuard('owner')], loadComponent: () => import('./features/pricing/beverages.page') },
    { path: 'stock',               canActivate: [roleGuard('owner', 'pizzaiolo')], loadComponent: () => import('./features/stock/stock.page') },
    { path: 'alerts',              canActivate: [roleGuard('owner')], loadComponent: () => import('./features/alerts/alerts.page') },
    { path: '**',                  redirectTo: 'dashboard' },
  ]},
];
```

## 3. Telas principais

### 3.1 Login / Cadastro
- `MatCard` centralizado; cadastro pede nome da pizzaria (cria tenant).
- Erros via snackbar.

### 3.2 Shell (layout)
- `mat-sidenav-container` com menu lateral:
  Dashboard · Alertas (badge com contador) · Operação (Pedidos, Produção, Lucro real) · Precificação ▾
  (Fichas técnicas, Meio a meio, Combos, Bebidas) · Cadastros ▾
  (Ingredientes, Preparos, Produtos, Tamanhos, Custos fixos, Taxas, Equipe) · Estoque
- Toolbar com nome da pizzaria + logout.

### 3.3 Ingredientes
- `mat-table` com busca (`mat-form-field` + filtro client-side sobre a página),
  colunas: nome, compra, custo/unidade, perda, estoque.
- `mat-dialog` para criar/editar; o custo/unidade é exibido calculado ao vivo no form.
- Aviso ao editar preço: "Alterações recalculam preparos, fichas e alertas."

### 3.4 Preparos (montador de receita)
- Form com nome, rendimento, unidade.
- Lista dinâmica de itens: `mat-autocomplete` (ingrediente OU preparo) + quantidade.
- Rodapé fixo com custo total e custo/unidade atualizando a cada digitação (via `/preparations/calculate` com debounce 300 ms).

### 3.5 Fichas técnicas — tela principal
- Stepper (`mat-stepper`): ① Produto + Tamanho → ② Ingredientes e preparos → ③ Custos (embalagem, perda, margem) → ④ Resultado.
- Painel lateral sticky com cards: **Custo · Preço sugerido · Lucro · Margem % · Status** (chip verde/vermelho) — atualiza ao vivo via `POST /pricing/preview` com debounce.

### 3.6 Precificação (listagem)
- `mat-table` de todas as fichas: sabor, tamanho, custo, preço, lucro, margem, status.
- Filtro por status (chips `mat-chip-listbox`: Todos / Vermelhos / Verdes).
- Ordenação por coluna.

### 3.7 Meio a meio / Quatro sabores
- Seletor de tamanho + 2 (ou 4) `mat-select` de sabores → resultado calculado on-demand.
- Card de resultado igual ao da ficha.

### 3.8 Combos
- Lista de combos + dialog de montagem: adicionar itens (pizza sabor+tamanho, bebida), desconto, preço fixo opcional.
- Resultado: custo, preço, lucro, margem, status.

### 3.9 Estoque
- Abas (`mat-tab-group`): Saldos | Movimentos.
- Saldos: tabela com chip "abaixo do mínimo" vermelho.
- Movimentos: form de entrada/saída (ingrediente, qtd, custo unitário, data) + histórico paginado.

### 3.10 Alertas
- Lista com `mat-expansion-panel` ou tabela: tipo, mensagem, data, ação "Marcar resolvido".

### 3.11 Dashboard
- Topo: 4–6 `mat-card` de indicadores (itens totais, itens no vermelho, custo fixo/mês, custo indireto/pizza, margem média, alertas abertos).
- Meio: dois rankings lado a lado (lucro ↑, custo ↑) com `mat-table`.
- Gráfico de barras de margem por sabor (`ng2-charts`).
- Consulta individual: `mat-select` produto + tamanho → composição detalhada do cálculo (breakdown).

### 3.12 Fila da Produção (`/producao` — `pizzaiolo` e `owner`)
- Kanban em 3 colunas: **Recebido → Em preparo → Pronto**, com contadores por coluna.
- Polling de 8s (`interval` + `switchMap`), pausado quando a aba está oculta (`visibilitychange`) e com indicador vivo **"Atualizado há Xs"**.
- Cards grandes touch-friendly para tablet 10": código (`#8F3K2Q`), chip do tipo (Entrega/Retirada/Balcão), relógio com cor semafórica (verde `<10 min`, amarelo `<20 min`, vermelho `≥20 min` + selo `ATRASADO`), itens com sabores e frações (`1/2`, `1/4`).
- Receita resumida expansível no card (ficha técnica somente leitura, sem custos/margens).
- Botão gigante de avançar status (Iniciar preparo → Marcar pronto → Saiu para entrega / Concluir) e cancelamento com confirmação de motivo (`CancelOrderDialog`).
- Aviso sonoro de pedido novo (toggle no cabeçalho, padrão ligado, destravado após o primeiro toque via Web Audio API + fallback `<audio>` WAV local).

### 3.13 Pedidos e Comanda Térmica (`/pedidos` e `/pedidos/:id/ticket` — `atendente` e `owner`, Sprint 4)
- Cabeçalho com cards de resumo do dia (`GET /orders/today`): total, recebidos, em preparo, prontos/entrega, concluídos e cancelados.
- Filtros por status, tipo de atendimento e busca por código/cliente.
- Visão master-detail com linha do tempo de transições, botões de avanço/cancelamento e botão **Imprimir comanda**.
- Modal **Novo pedido** (`NewOrderDialog`) otimizado para tablet 10": construtor de pizzas (1, 2 ou 4 sabores com prevalência do maior preço), combos e bebidas, carrinho em `signal` preservado ao fechar o modal e envio protegido por `Idempotency-Key`.
- Rota `/pedidos/:id/ticket` (`OrderTicketPage`) com layout de cupom térmico 80mm, `@media print` que oculta o restante da interface e rodapé `"Não é documento fiscal"`.

### 3.14 Site Público Mobile-First (`/menu/:slug`, `/checkout/:slug`, `/acompanhar/:slug/:code` — Sprint 5)
- Rotas independentes fora do `ShellComponent` (sem `authGuard`), responsivas a partir de 375px.
- `/menu/:slug` (`PublicMenuPage`): seletor de tamanho, montagem de pizza inteira ou meio a meio, combos, bebidas, barra fixa da sacola persistida em `localStorage` isolada por `slug` e estado amigável de pizzaria não encontrada (404).
- `/checkout/:slug` (`PublicCheckoutPage`): escolha entre Retirada e Entrega (com cálculo automático da taxa), dados do cliente, forma de pagamento informativa e envio com `Idempotency-Key`.
- `/acompanhar/:slug/:code` (`PublicTrackingPage`): código em destaque, barra visual de progresso e polling automático a cada 15s (pausado com aba oculta).

### 3.15 Relatório de Lucro Real (`/lucro` — `owner`, Sprint 6)
- Seletor rápido de período (**Hoje**, **Últimos 7 dias**, **Últimos 30 dias**, **Personalizado**).
- 6 cards de KPI: Faturamento bruto, Custo real de insumos (com conferência contra `StockMovement.OrderOut`), Taxa estimada de cartão (P8), Lucro bruto real, Margem bruta real % e Ticket médio.
- Card separado para **Resultado líquido após rateio de custos fixos** (P9).
- Gráfico de barras diário (`ng2-charts`: Faturamento × Custo × Lucro) e tabelas de ranking por produto/sabor e participação por canal (Balcão, Retirada, Entrega, Site público).

## 4. Serviços e modelos

```typescript
// core/models/pricing.models.ts
export interface PricingResult {
  directCost: number; indirectCost: number; totalCost: number;
  suggestedPrice: number; cardPrice?: number; taxValue: number;
  estimatedProfit: number; marginRealPercentage: number;
  marginTarget: number; status: 'Within' | 'NeedsAdjustment';
  breakdown: { label: string; cost: number }[];
}
```

- Um service por feature (`IngredientsService`, `PricingService`, `DashboardService`, …)
  expondo métodos tipados que retornam `Observable<T>`.
- `ErrorInterceptor` converte erros da API em snackbars em português.
- `JwtInterceptor` adiciona `Authorization: Bearer …` e trata 401 → refresh → retry.

## 5. Componentes compartilhados

| Componente | Uso |
|------------|-----|
| `MarginStatusChip` | chip verde "Dentro da margem" / vermelho "Precisa ajustar" |
| `MoneyInput` | input `matInput` com máscara de moeda e `parseFloat` seguro |
| `ConfirmDialog` | ` MatDialog` de confirmação de exclusão |
| Pipe `brl` | `R$ 13,94` |
| Pipe `pct` | `60,0%` |

## 6. Tratamento de estado

- RxJS padrão: services + `signal()` do Angular 18 para estado local simples de página.
- Sem NgRx (decisão ADR-2).
- "Recálculo em andamento": `BehaviorSubject<boolean>` no `PricingService` mostra
  `mat-progress-bar` indeterminada enquanto a API processa o recálculo em cadeia.

## 7. Tema e identidade

- `@use '@angular/material' as mat;` com custom theme:
  - Primária: vermelho pizzaria (`#C62828`)
  - Secundária: dourado (`#F9A825`)
  - Acento: branco/creme, fundo claro
- Density: `comfortable`; tipografia: Roboto.
- Ícones Material Symbols.
