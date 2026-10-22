# 10 — Sprints: Pedidos, Produção e Lucro Real

Extensão do produto: de “sistema que precifica” para “sistema que precifica **e opera**”
— pedidos (tablet do atendente + site público), painel do pizzaiolo, baixa automática
de estoque e relatório de lucro real.

Documento complementar ao [09 — Plano de Implementação](09-plano-de-implementacao.md)
(que cobre o MVP de precificação, já entregue). Mantém a mesma notação de checklist.

## Premissas (decididas com o cliente)

| # | Tema | Decisão |
|---|------|---------|
| P1 | Origem do pedido | Tablet do atendente (balcão/retirada/entrega) **e** site público com checkout sem login |
| P2 | Acompanhamento | Código alfanumérico de 6 caracteres (ex.: `#8F3K2Q`), digitado no site |
| P3 | Baixa de estoque | Ao concluir na produção (status **Pronto**); cancelamento posterior devolve estoque |
| P4 | Papéis | `owner` + `atendente` + `pizzaiolo` |
| P5 | Tempo real | Polling (8s) no painel da produção |
| P6 | Lucro | Faturamento (pedidos concluídos) − custo real de ingredientes |
| P7 | Pagamento | Sem gateway: **ticket/comanda imprimível** para cobrar manualmente |
| P8 | Estimativa de taxa de cartão | Campo opcional “forma de pagamento” no pedido (dinheiro/cartão/PIX), usado só para o ticket e para estimar a taxa via `FeeConfig.CardFeePercentage` |
| P9 | Custo fixo no relatório de lucro | Exibido como linha separada (“resultado após rateio”), sem alterar o lucro bruto |
| P10 | Aviso sonoro | Sim, no painel do pizzaiolo (toggle, padrão ligado, sem autoplay bloqueante) |

> P8–P10 são os pontos que ficaram em aberto; foram adotadas as opções recomendadas.
> Ajustar aqui antes da Sprint 4/6/3 respectivamente se o cliente mudar de ideia.

## Definition of Done (global)

- [x] `dotnet build` e `dotnet test` verdes (suítes existentes + novas).
- [x] `ng build --configuration production` sem erro.
- [x] Todo endpoint novo coberto por teste de integração (Api.Tests).
- [x] Toda regra de domínio nova coberta por teste unitário (Domain.Tests).
- [x] Multi-tenancy respeitado (nenhum endpoint lê/escreve fora do tenant do JWT; endpoints públicos resolvem tenant pelo **slug**, nunca pelo corpo).
- [x] Erros seguem o envelope padrão `{ status, message, errors[] }`.
- [x] Migração EF gerada e aplicável no SQLite (dev) e SQL Server (prod).
- [x] Frontend responsivo (tablet 10" e celular) e em PT-BR.
- [x] Docs `05`, `07`, `08` atualizadas na mesma sprint em que o código nasce.

## Velocidade assumida

Sprints de **2 semanas** com 1 dev full-stack. Estimativas em **story points** (Fibonacci);
a capacidade média assumida é de **20–24 pts/sprint**. O total (~120 pts) cabe em **7 sprints**.

## Visão geral

| Sprint | Nome | Objetivo | Entregável verificável | Pontos | Status |
|--------|------|----------|------------------------|--------|--------|
| 1 | Fundação: papéis e slug | Permissões por papel + resolução pública de tenant | Owner cria usuário com papel; `/public/{slug}` responde 200 | 20 | ✅ |
| 2 | Domínio de pedidos | Entidades, máquina de status e baixa de estoque | API aceita pedido e baixa estoque corretamente | 24 | ✅ |
| 3 | Fila da produção | Painel do pizzaiolo com polling | Pizzaiolo vê e move pedidos em tempo real | 18 | ✅ |
| 4 | Tablet do atendente | Criação de pedido + ticket imprimível | Atendente fecha pedido do balcão ao delivery em <1 min | 22 | ✅ |
| 5 | Site público | Cardápio, checkout anônimo e acompanhamento | Cliente final faz pedido pelo celular e acompanha | 22 | ✅ |
| 6 | Lucro real | Relatório de faturamento − custo real | Tela de lucro por período/produto/dia | 16 | ✅ |
| 7 | Hardening | Testes, seed, docs e polish | Release candidate documentado | 18 | ✅ |

---

## Sprint 1 — Fundação: papéis e slug (20 pts) — ✅ CONCLUÍDA

**Por quê:** nada do resto é seguro sem roles (hoje tudo é `owner`, claim gravada fixa em
`src/PizzariaLucrativa.Infrastructure/Identity/TokenService.cs:55`) e o site público
precisa de um tenant anônimo (`TenantContextMiddleware` só lê JWT).

### Backend (11 pts)
- [x] Seed dos roles `owner` / `atendente` / `pizzaiolo` no startup (idempotente) — `Infrastructure/Persistence/AppRoles.cs` + `RoleSeeder.cs`.
- [x] `TokenService` lê o papel real do usuário (`UserManager`) e emite a claim `role` (+ claim `role_list` se necessário).
- [x] Políticas de autorização registradas no `Program.cs` (`OwnerOnly`, `AtendenteOuOwner`, `ProducaoOuOwner`) + atributos nos controllers existentes que exponham custo/margem (Dashboard, Pricing, Reports) restritos a `owner`.
- [x] `POST /api/v1/users` + `GET /api/v1/users` + `DELETE /api/v1/users/{id}` (só owner, mesmo tenant) com validação: não derrubar o último owner. (+ `PUT /users/{id}` e `GET /users/roles`)
- [x] `Tenant.Slug` (string 40, único, gerado do nome no register e no seed; editável) + `PUT /tenant/slug` (via `PUT /tenant` com `{ slug }`).
- [x] `TenantContextMiddleware`: se não houver claim JWT, tenta resolver tenant pelo segmento `{slug}` da rota pública (valida contra o banco, 404 se inválido).
- [x] Migration EF (`AddSlugTenants`).

### Frontend (7 pts)
- [x] `roleGuard` (`core/guards/role.guard.ts`) + `AuthService` expondo papel e `isInRole()`.
- [x] Menu do shell filtrado por papel; pizzaiolo cai direto em `producao`; atendente em `pedidos`.
- [x] Feature `features/team` — lista de usuários + dialog para criar/editar papel (owner).
- [x] Tela de tenant: campo slug junto às taxas (owner).

### Testes e docs (2 pts)
- [x] Api.Tests: atendente não acessa `GET /dashboard/summary` (403); owner gerencia usuários; último owner não pode ser removido.
- [x] Api.Tests: rota pública com slug inexistente → 404; slug válido → tenant resolvido.
- [x] Atualizar `docs/04-arquitetura.md` (roles) e `docs/07-api.md` (users + public).

**CA da sprint:** owner cadastra um `atendente` e um `pizzaiolo`; cada um loga e vê **apenas** o menu do seu papel; endpoint público `/api/v1/public/{qualquer-slug-inexistente}/menu` responde 404 e um slug válido resolve o tenant.

---

## Sprint 2 — Domínio de pedidos e baixa de estoque (24 pts) — ✅ CONCLUÍDA

**Por quê:** é o núcleo; o restante é UI sobre esta API.

### Backend — modelo (8 pts)
- [x] Enum `OrderStatus` (`Received, InPreparation, Ready, OutForDelivery, Completed, Cancelled`) e `OrderType` (`Counter, Pickup, Delivery`).
- [x] `Order` (herda `TenantEntity`): `Code` (6 chars, único por tenant, gerado com alfabeto sem O/0/I/1), status, tipo, dados do cliente (nome, telefone, endereço: distrito/rua/nº/complemento), `DeliveryFee`, `Discount`, `Total`, `Notes`, `PlacedByUserId?` (null = público), `PaymentMethod?` (P8), timestamps `PlacedAt/StartedAt/ReadyAt/DeliveredAt/CancelledAt`, `CancelReason?`.
- [x] `OrderItem`: `SizeId?`, `Quantity`, `UnitPrice` (snapshot do preço), `CostSnapshot` (snapshot do custo), `Total`, `Notes?`.
- [x] `OrderItemFlavor`: até 4 por item, `ProductId`, `SharePercentage` (50/50 e 25/25/25/25).
- [x] `StockMovement`: coluna `OrderId?` + enum `StockMovementType.OrderOut`.
- [x] Migration + `AppDbContext` (TPT já resolvido; índices: `Order(Code)` único/tenant, `Order(Status, CreatedAt)`, `StockMovement(OrderId)`).
      ⚠️ **Desvio:** o TPT (base `TenantEntity`) impede índices compostos que atravessam tabelas. Adotado `Order(Code)` (não único, lookup) + `Order(Status, PlacedAt)`; unicidade do código por tenant garantida no `OrderService`.

### Backend — regras (10 pts)
- [x] `OrderService` (Application/Services): `CreateAsync` valida itens (produto/tamanho/ficha existente, share soma 100%), calcula `UnitPrice` pelo `CardPrice ?? SuggestedPrice` da ficha (ou `Combo.TotalPrice`), aplica taxa de entrega padrão de `FeeConfig.DeliveryFixedValue` quando `Delivery`, e grava `CostSnapshot`.
- [x] Máquina de status em `ChangeStatusAsync`: valida transições legais, preenche timestamps e exige motivo em cancelamento.
- [x] `StockConsumptionService` — **núcleo da baixa**:
  - item com ficha → expande `TechnicalSheetItem`;
  - `Ingredient` → baixa direta (conversão g↔kg, ml↔L com `UnitConverter` do Domain);
  - `Preparation` → expande `PreparationItem` recursivo: `consumo = qty_item × (qty_ing / YieldQuantity)`, reaproveitando a lógica de ciclos do `PricingCalculator`;
  - **mista** → cada sabor entra com seu `SharePercentage`;
  - agrupa por ingrediente e grava **um** `StockMovement(OrderOut)` por ingrediente com `UnitCost` atual;
  - dentro de transação; idempotente (segunda chamada de `ready` não baixa de novo);
  - `Beverage`: novo `Product.IngredientId?` opcional — se houver vínculo, debita também.
- [x] Cancelamento pós-`Ready` → estorno (movimento `In` com `OrderId` e nota “estorno do pedido #código”).
- [x] Fila de recálculo enfileirada após baixa (o `Alert(LowStock)` existente passa a disparar por venda).

### Backend — endpoints (2 pts)
- [x] `OrdersController`: `POST /orders`, `GET /orders` (filtros: status, tipo, data, busca por código/cliente, paginado), `GET /orders/{id}`, `POST /orders/{id}/cancel`.
- [x] `KitchenController`: `GET /kitchen/queue` (projeção enxuta com tempo decorrido).

> Antecipado da Sprint 3/4 porque os testes da Sprint 2 exercitam o fluxo por HTTP:
> `POST /kitchen/queue/{id}/start|ready` e `POST /orders/{id}/status`.

### Testes (4 pts)
- [x] Domain.Tests: expansão de preparo com conversão de unidade; preparo aninhado; mista 50/50; quatro sabores; cancelamento com estorno; idempotência do `ready`.
- [x] Api.Tests: fluxo completo (criar → start → ready → deliver) conferindo saldo de estoque antes/depois; transição ilegal → 400; pedido de outro tenant → 404.

**CA da sprint:** criar um pedido de 2 pizzas (uma simples, uma meio a meio) e um combo; ao marcar **Pronto**, os ingredientes caem exatamente na quantidade da ficha (com frações dos sabores mistos), um movimento `OrderOut` é gerado por ingrediente e o saldo exibido em `/stock/balances` reflete a baixa.

> **Entrega:** `OrderFlowTests` cobre simples e meio a meio (fração 50/50), baixa
> idempotente, estorno no cancelamento e isolamento por tenant; `StockUsageCalculatorTests`
> cobre conversão, preparo aninhado, mistas e ciclo. `docs/05` e `docs/07` atualizados.

---

## Sprint 3 — Fila da produção: painel do pizzaiolo (18 pts) — ✅ CONCLUÍDA

### Backend (3 pts)
- [x] `GET /kitchen/queue` enriquecido: itens com sabores e frações, tamanho, combos, observações, tempo decorrido desde `PlacedAt`/`StartedAt`, flag “atrasado” (≥20 min), contagem por status.
- [x] `POST /kitchen/queue/{id}/start|ready|finish|cancel` (alias autorizados a `pizzaiolo`) — mesma máquina de status do `OrderService`.

### Frontend (13 pts)
- [x] `features/kitchen` — rota `/producao` (pizzaiolo + owner), layout kanban em 3 colunas: **Recebido → Em preparo → Pronto**.
- [x] Polling de 8s com `interval` + `switchMap` (cancela no `ngOnDestroy`), pausa quando a aba está oculta (`visibilitychange`) e mostra “atualizado há Xs”.
- [x] Cards grandes touch-friendly: código, tipo (chip Entrega/Retirada/Balcão), tempo decorrido com cor (verde <10min, amarelo <20, vermelho ≥20), itens com sabores e frações; botão de avançar gigante.
- [x] Expandir card → receita resumida (ficha técnica daquele item, somente leitura).
- [x] Aviso sonoro de pedido novo (toggle no canto, Web Audio/`<audio>` com arquivo local, inicia após primeiro toque — P10).
- [x] Confirmação em cancelamentos/alertas de erro via `ErrorInterceptor` já existente.

### Testes e docs (2 pts)
- [x] Api.Tests (`KitchenQueueTests`): `pizzaiolo` acessa `/kitchen/queue` e `start/ready/finish`; `atendente` recebe 403; pedido ≥20 min sinalizado com `late=true`; nenhum campo de custo/margem exposto.
- [x] `docs/07-api.md` (kitchen) e `docs/08-frontend-angular.md` (rota `producao`).

**CA da sprint:** com o tablet do pizzaiolo aberto, um pedido criado pela API aparece sozinho em ≤8s, é movido para “Em preparo” e “Pronto” com um toque e some da fila; o relógio do card muda de cor conforme o tempo.

---

## Sprint 4 — Tablet do atendente: novo pedido + ticket (22 pts) — ✅ CONCLUÍDA

### Backend (5 pts)
- [x] `GET /orders/today` (resumo do dia: por status/tipo) para o cabeçalho da tela (+ `GET /orders/catalog` sem custos/margens para o construtor de pedidos do atendente).
- [x] Endpoint `GET /orders/{id}/ticket` → JSON pronto para impressão (pizzaria: nome, endereço/telefone, código, itens com frações, subtotal/taxa/desconto/total, “Não é documento fiscal”).
- [x] Idempotência do `POST /orders` via header `Idempotency-Key` (evita pedido duplicado em tablet com rede ruim).

### Frontend (12 pts)
- [x] `features/orders` — rota `/pedidos` (atendente + owner): lista do dia com filtros por status e busca por código/cliente.
- [x] Dialog **Novo pedido** (`NewOrderDialog`) otimizado para tablet 10":
  - passo 1: tipo (Balcão/Retirada/Entrega) e dados do cliente (endereço completo só para entrega, com taxa de entrega);
  - passo 2: catálogo por tamanho → toque no sabor; botão “meio a meio” e “quatro sabores” com seletor de frações;
  - passo 3: bebidas/combos;
  - passo 4: observações, desconto (owner/atendente), resumo e total;
  - stepper de quantidade gigante; carrinho persistido em `signal` (`OrdersService.draftCart`, resiste a troca de dialog).
- [x] Tela de detalhe do pedido: timeline de status com botões de transição (mesma máquina) + botão **Imprimir comanda**.
- [x] Impressão via rota `/pedidos/:id/ticket` (`OrderTicketPage`) com CSS `@media print` (esconde app, só comanda) — sem dependência de backend de impressão.

### Testes e docs (5 pts)
- [x] Api.Tests (`Sprints4To7IntegrationTests`): `Idempotency-Key` repetida devolve o mesmo pedido (não duplica); atendente cria e cancela pedido; desconto > valor do pedido → 400.
- [x] Domain.Tests (`ProfitAndOrderTotalsTests`): cálculo do total do pedido (itens + taxa − desconto) com arredondamento.
- [x] `docs/07` (orders/ticket) e `docs/08` (rota `pedidos`).

**CA da sprint:** no tablet, o atendente monta um pedido delivery de 1 pizza meio a meio + 1 bebida em menos de 1 minuto, imprime a comanda com o total certo e ela aparece na fila do pizzaiolo.

---

## Sprint 5 — Site público: cardápio, checkout e acompanhamento (22 pts) — ✅ CONCLUÍDA

### Backend (8 pts)
- [x] `PublicController` (`[AllowAnonymous]`, rota `/api/v1/public/{slug}`):
  - `GET /menu` — dados da pizzaria, tamanhos, sabores por tamanho com preço (`CardPrice ?? SuggestedPrice`), combos ativos, bebidas;
  - `POST /orders` — pedido anônimo (valida telefone/endereço de entrega, itens mínimos do cardápio — **nunca aceita preço/custo vindo do cliente**, sempre recalcula pelo servidor);
  - `GET /orders/{code}` — só status, primeiro nome do cliente, itens e timestamps (sem expor telefone/endereço a terceiros).
- [x] Rate limit dedicado (`public-orders`: 10 pedidos/min por IP + `public-tracking`: 30 consultas/min de status).
- [x] Geração de `Code` resistente a colisão e a adivinhação (6 chars de alfabeto seguro + checagem única por tenant).

### Frontend (12 pts)
- [x] Rotas **fora do shell** (sem `authGuard`, layout próprio mobile-first): `menu/:slug`, `checkout/:slug`, `acompanhar/:slug/:code`.
- [x] Cardápio (`PublicMenuPage`): seletor de tamanho que troca todos os preços, sabores com opção “meio a meio”, carrinho em `signal` persistido em `localStorage` (`PublicMenuService`).
- [x] Checkout (`PublicCheckoutPage`): nome, telefone, endereço (com taxa), observações, resumo e total; envio com `Idempotency-Key`; sucesso redireciona para acompanhamento com o código grande.
- [x] Acompanhamento (`PublicTrackingPage`): timeline (Recebido → Em preparo → Pronto → Saiu → Concluído) com polling de 15s pausado em aba oculta.
- [x] Página “Cardápio não encontrado” amigável.
- [x] Link do cardápio exibido no painel do owner (`fees.page.html`: URL + slug), com botão copiar.

### Testes e docs (2 pts)
- [x] Api.Tests (`Sprints4To7IntegrationTests`): pedido anônimo aceito; preço adulterado no corpo é ignorado (usa preço do servidor); código errado → 404; **isolamento**: código da pizzaria A não resolve na pizzaria B; rate limit → 429.
- [x] `docs/07` (seção public) e `docs/08` (rotas públicas).

**CA da sprint:** no celular, um cliente abre `/menu/{slug}`, monta um pedido delivery sem logar, recebe o código e vê o status avançar sozinho conforme o pizzaiolo move o pedido; o pedido cai na fila da produção normalmente.

---

## Sprint 6 — Lucro real (16 pts) — ✅ CONCLUÍDA

### Backend (7 pts)
- [x] `ReportService` + `GET /api/v1/reports/profit?from=&to=` (só owner): faturamento (pedidos `Completed` no período), custo real (Σ `CostSnapshot`, conferido contra Σ `StockMovement.OrderOut` no período), lucro bruto, margem %, nº de pedidos, ticket médio, taxa estimada de cartão (P8).
- [x] `GET /reports/profit/by-day` (série para gráfico), `/by-product` (ranking), `/by-channel` (balcão/retirada/entrega/site).
- [x] Linha “resultado após rateio” com o rateio mensal de `FixedCost` (P9) — separada do lucro bruto.
- [x] Exclusão/cancelamento considerado: pedidos cancelados saem do faturamento e do custo (e o estorno já devolveu o estoque).

### Frontend (7 pts)
- [x] `features/profit` — rota `/lucro` (owner): seletor de período (hoje/7d/30d/personalizado), cards (faturamento, custo de ingredientes, taxa estimada de cartão, lucro bruto, margem, ticket médio), gráfico de barras por dia (`ng2-charts`), ranking de produto e mix por canal.
- [x] Indicador de divergência faturamento × custo de estoque (alerta se bater diferente, sinaliza divergência de auditoria).

### Testes e docs (2 pts)
- [x] Api.Tests (`Sprints4To7IntegrationTests`): relatório só para owner (atendente recebe 403); pedidos cancelados fora; período com zero pedidos não divide por zero.
- [x] Domain.Tests (`ProfitAndOrderTotalsTests`): consolidação do período (faturamento − custo − estimativa de taxa + rateio fixo proporcional).
- [x] `docs/07` (reports) e `docs/08` (rota `lucro`).

**CA da sprint:** o owner abre `/lucro` no mês corrente e vê o lucro real consolidado com o custo que o estoque de fato sofreu, com gráfico por dia e ranking; o atendente recebe 403 na mesma rota.

---

## Sprint 7 — Hardening e release (18 pts) — ✅ CONCLUÍDA

### Qualidade (8 pts)
- [x] Suíte completa verde (45 Domain.Tests + 28 Api.Tests = 73 testes); cobertura do Domain medida (`dotnet test --collect:"XPlat Code Coverage"`).
- [x] Revisão de segurança: endpoints públicos (validação de slug/código, limites de tamanho, validação de telefone/endereço), confirmação automatizada de que nenhum custo/margem vaza para `atendente`/`pizzaiolo`/público.
- [x] Concorrência: dois pizzaiolos clicando “Pronto” ao mesmo tempo → apenas uma baixa (lock por pedido no `OrderService` + verificação idempotente transacional no `StockConsumptionService`, coberto por teste de chamadas paralelas).
- [x] Performance: `GET /kitchen/queue` e `GET /orders` com projeção enxuta e índices `Order(Code)` e `Order(Status, PlacedAt)`.

### Dados e UX (5 pts)
- [x] `DemoSeeder` estendido: 1 pizzaiolo (`pizzaiolo@pizzarialucrativa.com`) + 1 atendente (`atendente@pizzarialucrativa.com`) de demonstração, bebidas, combo e 15 pedidos concluídos distribuídos em 30 dias (com estoque já baixado) + 2 ativos hoje para o relatório de lucro e a fila abrirem populados.
- [x] Estados vazios e loading em todas as telas novas.
- [x] Acessibilidade básica: foco visível, contraste dos chips de status, labels e `aria-label` nos controles.

### Docs e entrega (5 pts)
- [x] Atualizar `docs/01` (produto agora opera vendas), `docs/02` (ES-05, ES-06 e módulo OP concluídos), `docs/05` (ER novo: Order/OrderItem/OrderItemFlavor/Slug), `docs/09` (marcar fases entregues) e `README.md` (novas rotas, roles, usuários demo).
- [x] `docker-compose` e builds de produção validados (`dotnet test` + `ng build --configuration production`).
- [x] Checklist de release documentado: backup do banco, troca de `Jwt__Key`, seed desligado em produção.

**CA da sprint:** `docker compose up --build` sobe com dados demo completos; um pedido criado no site passa por todo o fluxo (fila → baixa → entrega → aparece no relatório de lucro); suíte de testes 100% verde.

---

## Dependências entre sprints

```
Sprint 1 (roles + slug)
   └── Sprint 2 (pedidos + baixa)
         ├── Sprint 3 (painel pizzaiolo)   ──┐
         ├── Sprint 4 (tablet atendente)    ──┼── Sprint 6 (lucro, precisa de pedidos reais)
         └── Sprint 5 (site público)        ──┘
Sprint 7 (hardening) — após todas
```

- Sprints 3, 4 e 5 são **paralelizáveis** se houver mais de um dev (todas consomem a API da Sprint 2); em 1 dev, seguem na ordem 3 → 4 → 5.
- A Sprint 6 pode começar no backend logo após a 2, mas a consolidação só faz sentido com os fluxos das 3–5 validados.

## Riscos e mitigações

| Risco | Impacto | Mitigação |
|-------|---------|-----------|
| Expansão recursiva de preparos na baixa gera consumo errado (unidade/ciclo) | Estoque inconsistente | Testes Domain casos extremos; reaproveitar `UnitConverter` e a detecção de ciclos do `PricingCalculator`; modo “simular baixa” (dry-run) antes do rollout |
| Duas conclusões simultâneas baixam estoque em dobro | Duplicação de consumo | `ExecuteUpdate`/lock transacional + constraint de idempotência; teste de concorrência |
| Código de acompanhamento adivinhável | Vazamento de pedido | Alfabeto seguro, checagem por tenant e rate limit na consulta |
| Polling sobrecarrega com muitos pedidos | Fila lenta | Projeção enxuta, índice em `Status+CreatedAt`, polling pausado com aba oculta |
| Preço do cliente adulterado no site | Venda abaixo do custo | Servidor ignora preço recebido e recalcula pelo catálogo (CA Sprint 5) |
| Último owner removido/travado | Pizzaria sem admin | Validação “último owner” (Sprint 1) |
