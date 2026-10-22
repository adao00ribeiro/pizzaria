# 06 — Regras de Negócio e Motor de Cálculo

Este documento é a **especificação exata** da `PricingCalculator`
(`Domain/Services`). Os testes unitários devem cobrir cada fórmula com os
casos de referência indicados.

## 1. Custo por unidade de uso do ingrediente

```
UnitCost = (PurchasePrice ÷ PurchaseQuantity) × (1 + LossPercentage/100)
```

- `UnitCost` é sempre expresso **por unidade de uso** (1 g, 1 ml, 1 un).
- Exceção de unidade: se `PurchaseUnit ∈ {kg, L}` e a quantidade de uso for
  `g`/`ml`, converter na fronteira (`1 kg = 1000 g`, `1 L = 1000 ml`).
- Caso de teste: 5000 g a R$ 32,90, perda 3% → `0,006798`.

## 2. Custo do preparo (recursivo)

```
Preparation.TotalCost = Σ (Item.Quantity × Item.UnitCost)
  onde Item.UnitCost =
      se item é Ingrediente → Ingredient.UnitCost
      se item é Preparo     → Preparation.UnitCost (resolvido recursivamente)

Preparation.UnitCost = TotalCost ÷ YieldQuantity
```

- **Detecção de ciclos**: pilha de IDs visitados; repetição → `CycleDetectedException` com a trilha.
- Limite de profundidade: 5 níveis (massa dentro de recheio dentro de ficha é suficiente).
- Caso de teste: massa broto (10 un, R$ 11,10) → `UnitCost = 1,11`.

## 3. Custo direto da ficha técnica

```
TechnicalSheet.DirectCost =
    Σ (TechnicalSheetItem.Quantity × Item.UnitCost)      ← ingredientes e preparos
  + PackagingCost
  × (1 + TechnicalSheet.LossPercentage/100)              ← perda adicional, se houver
```

- A perda da ficha incide sobre o subtotal (ingrediente + preparo + embalagem) — simples e conservador.
- Caso de teste: ficha "Calabresa Broto" → `DirectCost = 3,88` (ver exemplo em [05 — Modelo de Dados, §5](05-modelo-de-dados.md)).

## 4. Custo indireto (rateio de custos fixos)

```
Método PerPizza:
  IndirectCost = Σ FixedCost.MonthlyValue ÷ ExpectedPizzasPerMonth

Método RevenuePercentage:
  IndirectCost = (Σ FixedCost.MonthlyValue ÷ ExpectedMonthlyRevenue) × 100
                 → vira percentual aplicado no passo 5 (entra como "margem alvo reduzida")
```

- **Decisão de design**: custos com método `PerPizza` viram valor fixo por pizza;
  custos `RevenuePercentage` acumulam num percentual `FixedOverhead%` que reduz
  a margem disponível (ver fórmula 5).
- Caso de teste: R$ 3.200/mês, previsão 2.500 pizzas → `1,28`.

## 5. Preço sugerido — margem sobre o preço de venda

A margem desejada é sobre o **preço cobrado do cliente** (correto para
delivery, pois cartão e aplicativo incidem sobre o preço, não sobre o custo).

```
CustoTotal = DirectCost + IndirectCost                     (se IndirectCost for valor)
Taxas%     = CardFee% + DeliveryFee%
Disponível = 1 − MargemDesejada/100 − Taxas%/100 − FixedOverhead%/100

SuggestedPrice = CustoTotal ÷ Disponível
```

- Guarda: `Disponível ≤ 0` → `DomainException("Margem + taxas atingem 100% — preço impossível")`.
- `MargemDesejada` = `TechnicalSheet.MarginPercentage` se preenchida, senão a padrão do tenant.
- Caso de teste: `(3,88 + 1,28) ÷ (1 − 0,60 − 0,0299) = 13,94`.

## 6. Lucro, margem real e status

```
TaxasValor   = SuggestedPrice × Taxas%/100
Profit       = SuggestedPrice − CustoTotal − TaxasValor
MarginReal%  = (Profit ÷ SuggestedPrice) × 100

Status = Within          se MarginReal% ≥ MargemDesejada − 0,01 (tolerância)
       = NeedsAdjustment caso contrário
```

- Caso de teste: `Profit = 8,36`, `MarginReal = 60,0%` → `Within`.

## 7. Redondeza de preço (opcional, configurável)

Para o preço "de cardápio" costuma-se arredondar para terminar em ,90 / ,99:

```
RoundedPrice = floor(SuggestedPrice) + 0,90    (se fração < 0,90)
```

- **Não aplicar automaticamente no MVP**; expor como coluna `CardPrice`
  (preço de cardápio sugerido) sem alterar o `SuggestedPrice` de cálculo.
- Documentado como configuração futura.

## 8. Pizzas mistas

Regra padrão (configurável — RF MS-04):

```
Meio a meio (2 sabores, mesmo tamanho):
  CostMista      = (CostA + CostB) ÷ 2
  PriceMista     = max(PriceA, PriceB)
  ProfitMista    = PriceMista − CostMista − PriceMista × Taxas%/100
  MarginMista%   = ProfitMista ÷ PriceMista × 100

Quatro sabores:
  CostMista      = (CostA + CostB + CostC + CostD) ÷ 4
  PriceMista     = max(PriceA, PriceB, PriceC, PriceD)
```

- Exige que os sabores tenham ficha para o **mesmo tamanho**; caso contrário, erro de validação.
- A mista é **calculada on-demand** (não é entidade persistida no MVP).

## 9. Combos

```
Combo.TotalCost  = Σ (Item.Quantity × Item.Cost) − DiscountValue
  Item.Cost = TechnicalSheet.DirectCost + IndirectCost   (pizzas)
            |  Beverage.PurchasePrice                     (bebidas)

Combo.TotalPrice = (Σ Item.Price × Quantity) − DiscountValue   se FixedPrice = null
                 | FixedPrice                                    caso contrário

Combo.TaxValue   = TotalPrice × Taxas%/100
Combo.Profit     = TotalPrice − TotalCost − TaxValue
Combo.Margin%    = Profit ÷ TotalPrice × 100
```

- `TotalCost − DiscountValue < 0` → erro: desconto maior que o custo do combo.

## 10. Bebidas

```
Beverage.SuggestedPrice = PurchasePrice × MarkupMultiplier   (multiplicador cadastrado)
```

- Bebida não tem ficha técnica nem rateio de custos fixos (simples por decisão).
- Markup padrão sugerido: 100% (dobro do custo), configurável.

## 11. Recálculo em cadeia

```
evento: preço de ingrediente muda (ou custo fixo, ou taxas)
  1. Ingredient.CostVersion++
  2. preparos que usam o ingrediente → recalcula TotalCost/UnitCost
  3. fichas técnicas que usam preparo ou ingrediente → recalcula tudo (§3–6)
  4. mistas → recalculadas on-demand (dependem das fichas)
  5. combos que referenciam fichas alteradas → recalcula (§9)
  6. para cada item que virou NeedsAdjustment → cria Alert(MarginBelow)
     (somente se não houver alerta aberto igual — UNIQUE parcial)
  7. para cada ingrediente com CurrentStock < MinimumStock → Alert(LowStock)
```

## 12. Casos de teste obrigatórios (golden files)

| # | Caso | Esperado |
|---|------|----------|
| 1 | UnitCost com perda 3% | 0,006798 |
| 2 | UnitCost conversão kg→g | ×0,001 |
| 3 | Preparo simples | 1,11/un |
| 4 | Preparo aninhado (2 níveis) | valor encadeado |
| 5 | Ciclo em preparo | CycleDetectedException |
| 6 | Ficha com perda adicional | subtotal × 1,02 |
| 7 | Rateio PerPizza | 1,28 |
| 8 | Rateio RevenuePercentage | percentual correto |
| 9 | Preço por margem 60% | 13,94 |
| 10 | Margem + taxas = 100% | DomainException |
| 11 | Status Within (tolerância) | Within |
| 12 | Status NeedsAdjustment | NeedsAdjustment |
| 13 | Meio a meio custo médio | (A+B)/2 |
| 14 | Meio a meio preço pelo maior | max(A,B) |
| 15 | Combo com desconto | lucro correto |
| 16 | Combo com preço fixo | usa FixedPrice |
| 17 | Recálculo em cadeia | 1 evento → N itens atualizados |
