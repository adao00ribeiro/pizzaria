# 09 — Plano de Implementação

Ordem de construção em 8 fases. Cada fase tem entregas verificáveis.
O backend avança primeiro nas fases de domínio; o frontend é construído em
paralelo (a partir da fase 2), seguindo os contratos da API já definidos.

## Fase 1 — Fundação

**Backend**
- [x] Criar solution com os 4 projetos (`Domain`, `Application`, `Infrastructure`, `Api`) + projetos de teste.
- [x] EF Core: `AppDbContext`, `TenantEntity` base, todas as entidades do [modelo de dados](05-modelo-de-dados.md), primeira migration.
- [x] ASP.NET Identity + JWT (access/refresh), `POST /auth/register` cria usuário + tenant + `FeeConfig` padrão.
- [x] `ITenantProvider` + query filter global + teste de isolamento de tenant.
- [x] Middleware de exceções padronizado + Serilog.

**Frontend**
- [x] Scaffold `ng new` com Angular Material, tema customizado.
- [x] Shell (sidenav + toolbar), login, register, guards e interceptors.

**Entrega**: usuário cadastra pizzaria, loga e vê o shell vazio com menu.

## Fase 2 — Ingredientes e Preparos

**Backend**
- [x] CRUD `/ingredients` + cálculo de `UnitCost` (fórmula §1 das regras).
- [x] CRUD `/preparations` com itens recursivos + detecção de ciclos + `UnitCost`.
- [x] Testes unitários: casos 1–5 da tabela de golden files.

**Frontend**
- [x] Tela de ingredientes (tabela + dialog + custo ao vivo).
- [x] Tela de preparos (montador de receita com autocomplete).

**Entrega**: cadastro de insumos e massa/molho com custo unitário correto.

## Fase 3 — Custos fixos, taxas, produtos e tamanhos

**Backend**
- [x] CRUD `/fixedcosts` com os dois métodos de rateio.
- [x] `GET/PUT /fees` (configuração do tenant).
- [x] CRUD `/products` e `/sizes`.

**Frontend**
- [x] Telas de custos fixos, taxas (form único) e produtos/tamanhos.

**Entrega**: base de custos e catálogo prontos.

## Fase 4 — Fichas técnicas + motor de precificação ⭐ (núcleo)

**Backend**
- [x] `PricingCalculator` completo (fórmulas §3–§6 das regras).
- [x] CRUD `/technicalsheets` + `POST /technicalsheets/{id}/calculate` + `POST /pricing/preview`.
- [x] Testes unitários: casos 6–12 golden files.

**Frontend**
- [x] Stepper da ficha técnica com resultado ao vivo (PricingResult).
- [x] Listagem de precificação com chips de status e filtros.

**Entrega**: o produto faz o essencial — custo, preço, lucro, margem e status por pizza.

## Fase 5 — Mistas, combos e bebidas

**Backend**
- [x] `POST /pricing/half-half` e `/pricing/four-flavors` (fórmulas §8).
- [x] CRUD `/combos` + cálculo (fórmulas §9).
- [x] Bebidas com markup (fórmula §10).
- [x] Testes: casos 13–16.

**Frontend**
- [x] Telas de meio a meio/quatro sabores, combos e bebidas.

**Entrega**: cardápio completo com combinações precificadas.

## Fase 6 — Recálculo em cadeia, alertas e dashboard

**Backend**
- [x] `RecalculationService` (BackgroundService + Channel) — fluxo do [§5 da arquitetura](04-arquitetura.md).
- [x] Geração de `Alert(MarginBelow)` + endpoints de alertas.
- [x] `GET /dashboard/summary` e `/dashboard/rankings`.
- [x] Teste: caso 17 (recálculo em cadeia).

**Frontend**
- [x] Dashboard (cards, rankings, consulta individual, gráfico de margem).
- [x] Página de alertas + badge no menu.

**Entrega**: sistema alerta quando o fornecedor reajusta — o grande diferencial da oferta.

## Fase 7 — Estoque

**Backend**
- [x] CRUD `/stock/movements`, `GET /stock/balances`, alerta `LowStock`.
- [x] `CurrentStock` derivado dos movimentos.

**Frontend**
- [x] Tela de estoque com abas Saldos/Movimentos e alerta de reposição.

**Entrega**: bônus 03 da oferta original coberto pelo sistema.

## Fase 8 — Polimento e entrega

- [x] Testes de integração dos endpoints (casos obrigatórios NF-T02/T03).
- [x] Tratamento de erros e estados de loading/vazio em todas as telas.
- [x] Responsividade (uso no celular).
- [x] Dockerfile backend + build de produção do frontend.
- [x] Seed de demonstração (pizzaria fictícia com 10 sabores precificados).

## Riscos e mitigações

| Risco | Mitigação |
|-------|-----------|
| Fórmula de preço não bater com a expectativa do usuário | Exibir sempre o *breakdown* do cálculo e permitir sobrescrever margem por ficha |
| Recálculo em cadeia lento em tenant grande | Cálculo em background + cache por `CostVersion` + aviso na UI |
| Erro de isolamento de tenant | Teste automatizado obrigatório desde a fase 1 |
| Ciclo em preparos aninhados | Detecção com pilha + mensagem clara |
| Usuário digita unidade errada (kg vs g) | Conversão de unidades na fronteira + validação de unidade |

## Critérios de aceite globais (antes de considerar v1 pronta)

1. Cadastrar uma pizzaria do zero e precificar 10 sabores em < 30 min.
2. Todos os 17 golden files passando.
3. Teste de isolamento de tenant passando.
4. Simulação: aumentar preço de muçarela 20% → alertas aparecem nos itens afetados em < 5 s.
5. Fluxo completo funcionando em viewport mobile (375 px).
