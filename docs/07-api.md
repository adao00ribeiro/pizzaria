# 07 — API REST

- Base: `/api/v1`
- Todas as rotas (exceto auth e `/public/{slug}/…`) exigem JWT; o `TenantId` vem do token.
- Autorização por papel (`owner` / `atendente` / `pizzaiolo`): `OwnerOnly`, `AtendenteOuOwner`, `ProducaoOuOwner`.
- Respostas de erro padronizadas: `{ "status": 400, "message": "...", "errors": [{ "field": "...", "detail": "..." }] }`
- Paginação: `?page=1&pageSize=20` → resposta envelopada `{ "items": [...], "total": 123 }`.

## 1. Autenticação (público)

| Método | Rota | Body | Resposta |
|--------|------|------|----------|
| POST | `/auth/register` | `{ email, password, fullName, pizzeriaName }` | `{ accessToken, refreshToken, expiresIn }` |
| POST | `/auth/login` | `{ email, password }` | `{ accessToken, refreshToken, expiresIn }` |
| POST | `/auth/refresh` | `{ refreshToken }` | `{ accessToken, refreshToken, expiresIn }` |

## 2. Ingredientes

| Método | Rota | Observação |
|--------|------|------------|
| GET | `/ingredients?search=&page=` | lista com `unitCost` calculado |
| GET | `/ingredients/{id}` | detalhe |
| POST | `/ingredients` | cria; devolve `unitCost` |
| PUT | `/ingredients/{id}` | atualiza; dispara recálculo em cadeia |
| DELETE | `/ingredients/{id}` | 409 se em uso |
| GET | `/ingredients/{id}/usages` | lista preparos/fichas que o usam |

Body POST/PUT:
```json
{
  "name": "Muçarela",
  "purchaseUnit": "g",
  "purchaseQuantity": 5000,
  "purchasePrice": 32.90,
  "lossPercentage": 3,
  "minimumStock": 2000
}
```

## 3. Preparos

| Método | Rota | Observação |
|--------|------|------------|
| GET | `/preparations` | lista com `unitCost` |
| POST | `/preparations` | cria com itens |
| PUT | `/preparations/{id}` | atualiza |
| DELETE | `/preparations/{id}` | 409 se em uso |
| POST | `/preparations/{id}/calculate` | recalcula e retorna custo |

```json
{
  "name": "Massa broto",
  "yieldQuantity": 10,
  "yieldUnit": "un",
  "items": [
    { "type": "Ingredient", "ingredientId": "…", "quantity": 3000, "unit": "g" },
    { "type": "Preparation", "preparationId": "…", "quantity": 1, "unit": "un" }
  ]
}
```

## 4. Custos fixos e taxas

| Método | Rota |
|--------|------|
| GET/POST | `/fixedcosts` |
| PUT/DELETE | `/fixedcosts/{id}` |
| GET/PUT | `/fees` | configuração única do tenant (taxas + margem padrão) |

```json
// PUT /fees
{
  "cardFeePercentage": 2.99,
  "deliveryFeePercentage": 0,
  "deliveryFixedValue": 0,
  "defaultMarginPercentage": 60,
  "lossPercentageGlobal": 0
}
```

## 5. Produtos e tamanhos

| Método | Rota |
|--------|------|
| GET/POST | `/products` (`?category=Pizza`) |
| PUT/DELETE | `/products/{id}` |
| GET/POST | `/sizes` |
| PUT/DELETE | `/sizes/{id}` |

## 6. Fichas técnicas e precificação

| Método | Rota | Observação |
|--------|------|------------|
| GET | `/technicalsheets?status=Within&page=` | lista precificada com status |
| GET | `/technicalsheets/{id}` | detalhe com itens e resultado |
| POST | `/technicalsheets` | cria produto × tamanho com itens |
| PUT | `/technicalsheets/{id}` | atualiza |
| DELETE | `/technicalsheets/{id}` | |
| POST | `/technicalsheets/{id}/calculate` | força cálculo e retorna o resultado completo |
| POST | `/pricing/preview` | **calcular sem salvar** (form reativo da UI) |

`POST /pricing/preview` — o mais importante para a UI ao vivo:
```json
// request
{
  "productId": "…", "sizeId": "…",
  "packagingCost": 0.60, "lossPercentage": null, "marginPercentage": 60,
  "items": [ { "type": "Preparation", "preparationId": "…", "quantity": 1, "unit": "un" },
             { "type": "Ingredient", "ingredientId": "…", "quantity": 180, "unit": "g" } ]
}
// response
{
  "directCost": 3.88, "indirectCost": 1.28, "totalCost": 5.16,
  "suggestedPrice": 13.94, "cardPrice": 13.90,
  "taxValue": 0.42, "estimatedProfit": 8.36,
  "marginRealPercentage": 60.0, "marginTarget": 60,
  "status": "Within",
  "breakdown": [ { "label": "Massa broto", "cost": 1.11 }, ... ]
}
```

## 7. Pizzas mistas (calculadas on-demand)

| Método | Rota |
|--------|------|
| POST | `/pricing/half-half` |
| POST | `/pricing/four-flavors` |

```json
// POST /pricing/half-half
{ "sizeId": "…", "productIds": ["…", "…"] }
// response: mesmo shape de PricingResult + "flavors": [{ name, cost, price }]
```

## 8. Combos

| Método | Rota |
|--------|------|
| GET/POST | `/combos` |
| PUT/DELETE | `/combos/{id}` |
| POST | `/combos/{id}/calculate` | recalcula |
| POST | `/pricing/combos/preview` | preview sem salvar |

```json
{
  "name": "Combo Família", "discountValue": 5, "fixedPrice": null,
  "items": [
    { "productId": "…", "sizeId": "…", "quantity": 1 },
    { "productId": "bebida-id", "sizeId": null, "quantity": 2 }
  ]
}
```

## 9. Bebidas

| Método | Rota | Observação |
|--------|------|------------|
| GET | `/beverages` | produtos `Category=Beverage` com preço calculado |
| PUT | `/beverages/{id}/markup` | `{ "markupMultiplier": 2.0 }` |

## 10. Dashboard

| Método | Rota | Resposta |
|--------|------|----------|
| GET | `/dashboard/summary` | `{ totalItems, itemsInRed, monthlyFixedCost, indirectCostPerPizza, averageMargin, openAlerts }` |
| GET | `/dashboard/rankings?by=profit\|cost&direction=desc&page=` | `[{ name, size, value, marginStatus }]` |
| GET | `/dashboard/individual?productId=&sizeId=` | detalhe do cálculo (mesmo shape de `/technicalsheets/{id}`) |
| GET | `/dashboard/margins-chart` | `[{ name, marginPercentage }]` p/ gráfico (C) |

## 11. Alertas

| Método | Rota | Observação |
|--------|------|------------|
| GET | `/alerts?status=Open&type=MarginBelow&page=` | lista |
| POST | `/alerts/{id}/resolve` | marca resolvido |
| GET | `/alerts/count` | badge no menu |

## 12. Estoque

| Método | Rota | Observação |
|--------|------|------------|
| GET | `/stock/balances?belowMinimum=true&page=` | saldo por ingrediente |
| POST | `/stock/movements` | `{ ingredientId, type: "In", quantity, unitCost, note }` |
| GET | `/stock/movements?ingredientId=&page=` | histórico |

Movimentos `OrderOut` (baixa por pedido) e `In` (estorno de cancelamento) são
gravados automaticamente pelo fluxo de pedidos — ver §16.

## 13. Usuários (equipe) — só `owner`

| Método | Rota | Body | Observação |
|--------|------|------|------------|
| GET | `/users` | — | usuários do tenant |
| GET | `/users/roles` | — | papéis disponíveis |
| POST | `/users` | `{ email, password, fullName, role }` | cria usuário com papel |
| PUT | `/users/{id}` | `{ fullName?, role? }` | último owner não pode ser rebaixado (409) |
| DELETE | `/users/{id}` | — | último owner não pode ser removido (409) |

## 14. Tenant / perfil

| Método | Rota | Observação |
|--------|------|------------|
| GET | `/tenant` | dados da pizzaria (`id`, `name`, `slug`, `createdAt`) |
| PUT | `/tenant` | `{ name?, slug? }` — slug único (409 se em uso) e validado (400) |

## 15. Público (sem autenticação — docs/10 Sprint 5)

O tenant é resolvido exclusivamente pelo segmento `{slug}` da rota; slug inexistente → `404`
com envelope padrão. Nenhum campo de custo ou margem é exposto.

| Método | Rota | Observação |
|--------|------|------------|
| GET | `/public/{slug}/menu` | cardápio público completo (tamanhos, pizzas com preços por tamanho, combos, bebidas e taxa de entrega) |
| POST | `/public/{slug}/orders` | checkout anônimo (`Pickup` ou `Delivery`, ignora preços do cliente e recalcula no servidor; suporta header `Idempotency-Key`; rate limit 10/min/IP) |
| GET | `/public/{slug}/orders/{code}` | acompanhamento público pelo código de 6 caracteres (exibe apenas o primeiro nome do cliente, itens, totais e linha do tempo; rate limit 30/min/IP) |

## 16. Pedidos — `atendente` e `owner` (docs/10 Sprints 2 e 4)

| Método | Rota | Observação |
|--------|------|------------|
| POST | `/orders` | cria o pedido com snapshot de preço/custo e código de acompanhamento (suporta header `Idempotency-Key`) |
| GET | `/orders?status=&type=&from=&to=&search=&page=` | lista paginada (busca por código/cliente) |
| GET | `/orders/today` | resumo de contagens do dia (`totalOrders`, `receivedCount`, `inPreparationCount`, `readyCount`, `outForDeliveryCount`, `completedCount`, `cancelledCount`) |
| GET | `/orders/catalog` | catálogo operacional para o modal "Novo pedido" (tamanhos, pizzas com preço de venda, combos, bebidas, taxa de entrega — sem custos/margens) |
| GET | `/orders/{id}` | detalhe completo com itens e histórico de status (não expõe custo/margem) |
| GET | `/orders/{id}/ticket` | comanda térmica pronta para impressão (`disclaimer: "Não é documento fiscal"`) |
| POST | `/orders/{id}/status` | `{ status, note? }` — transição pela máquina de status (protegido contra concorrência) |
| POST | `/orders/{id}/cancel` | `{ reason }` — motivo obrigatório; estorna o estoque se já baixado |

```json
// POST /orders (Header opcional: Idempotency-Key: <uuid>)
{
  "type": "Counter",            // Counter | Pickup | Delivery
  "customerName": null,
  "customerPhone": null,
  "deliveryAddress": null,
  "notes": null,
  "paymentMethod": "Cash",      // opcional (P8): Cash | Card | Pix
  "discount": 0,
  "items": [
    {
      "sizeId": "…", "comboId": null, "quantity": 1, "notes": null,
      "flavors": [ { "productId": "…", "sharePercentage": 50 },
                   { "productId": "…", "sharePercentage": 50 } ]
    }
  ]
}
```

- Preço: `CardPrice ?? SuggestedPrice` da ficha (mista = **maior** sabor);
  custo: média dos sabores (mista). Bebida sem ficha usa `PurchasePrice × MarkupMultiplier`.
- `Delivery` aplica `FeeConfig.DeliveryFixedValue` (+ percentual quando configurado) e exige nome, telefone e endereço.
- Desconto > subtotal → `400`.
- Transições legais: `Received → InPreparation|Cancelled`;
  `InPreparation → Ready|Cancelled`; `Ready → OutForDelivery (Delivery)|Completed|Cancelled`;
  `OutForDelivery → Completed|Cancelled`. Cancelar exige `reason` (`400` sem ele).

## 17. Cozinha — `pizzaiolo` e `owner` (docs/10 Sprint 2/3)

| Método | Rota | Observação |
|--------|------|------------|
| GET | `/kitchen/queue` | fila ativa (`Received`, `InPreparation`, `Ready`) com contagens, itens, sabores/frações, receita resumida, tempo decorrido e flag `late` (≥20 min) |
| POST | `/kitchen/queue/{id}/start` | move para `InPreparation` (preenche `startedAt`) |
| POST | `/kitchen/queue/{id}/ready` | move para `Ready` — **dispara a baixa de estoque** (idempotente) |
| POST | `/kitchen/queue/{id}/finish` | despacha/conclui pedido `Ready` (`Delivery → OutForDelivery`; demais → `Completed`), retirando-o da fila |
| POST | `/kitchen/queue/{id}/cancel` | `{ reason }` — cancela a partir da cozinha (estorna estoque caso já baixado) |

```json
// GET /kitchen/queue (sem qualquer campo de custo/preço/margem)
{
  "counts": { "received": 1, "inPreparation": 0, "ready": 0, "total": 1 },
  "generatedAt": "2026-10-19T18:30:00Z",
  "items": [
    {
      "id": "…", "code": "8F3K2Q", "status": "Received", "type": "Delivery",
      "customerName": "Maria", "notes": "Sem cebola",
      "placedAt": "…", "startedAt": null, "readyAt": null,
      "elapsedMinutes": 4, "late": false,
      "items": [
        {
          "quantity": 1, "sizeName": "Grande", "comboName": null, "notes": null,
          "flavors": [
            {
              "productName": "Muçarela", "sharePercentage": 50,
              "recipe": [ { "label": "Muçarela", "quantity": 180, "unit": "g" } ]
            },
            {
              "productName": "Calabresa", "sharePercentage": 50,
              "recipe": [ { "label": "Calabresa", "quantity": 100, "unit": "g" } ]
            }
          ]
        }
      ]
    }
  ]
}
```

A baixa expande ficha técnica → insumo, preparos aninhados (com conversão de
unidade e detecção de ciclo), frações de mistas e combos, agrupando por
ingrediente: um `StockMovement(OrderOut)` por ingrediente.

## 18. Relatórios de Lucro Real — só `owner` (docs/10 Sprint 6)

| Método | Rota | Observação |
|--------|------|------------|
| GET | `/reports/profit?from=&to=` | consolidado do período: faturamento (`Completed`), custo real (`CostSnapshot`), conferência contra baixas líquidas de estoque (`stockOutCost`, `hasCostDivergence`), taxa estimada de cartão (P8), lucro bruto, margem %, ticket médio e resultado líquido após rateio proporcional de custos fixos (P9) |
| GET | `/reports/profit/by-day?from=&to=` | série diária (`date`, `ordersCount`, `revenue`, `cost`, `grossProfit`, `marginPercentage`) |
| GET | `/reports/profit/by-product?from=&to=` | ranking de produtos/sabores e combos vendidos no período |
| GET | `/reports/profit/by-channel?from=&to=` | participação por canal (`Counter`, `Pickup`, `Delivery`, `PublicSite`) |
