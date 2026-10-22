# 02 — Requisitos Funcionais

Legenda de prioridade: **M** = MVP, **C** = segunda leva, **F** = futuro.

## Módulo 1 — Autenticação e Tenant

| ID | Requisito | Prior. |
|----|-----------|--------|
| AU-01 | O usuário pode se cadastrar informando e-mail, senha e o nome da pizzaria (cria o tenant) | M |
| AU-02 | O usuário pode entrar com e-mail e senha e recebe access token (curto) + refresh token | M |
| AU-03 | Todo dado gravado é isolado por `TenantId`; um usuário nunca lê dados de outro tenant | M |
| AU-04 | O cadastro cria automaticamente um tenant com configuração padrão de taxas e margem desejada | M |
| AU-05 | O usuário pode editar os dados da pizzaria e a configuração padrão de precificação | M |

## Módulo 2 — Ingredientes

| ID | Requisito | Prior. |
|----|-----------|--------|
| IN-01 | Cadastrar ingrediente com: nome, unidade de compra (g, ml, un, kg, L), quantidade de compra, preço da compra e % de perda | M |
| IN-02 | O sistema calcula e exibe o **custo por unidade de uso** (ex.: R$ por grama) | M |
| IN-03 | Editar e excluir ingrediente (exclusão bloqueada se usado em preparo ou ficha — exige desvincular antes) | M |
| IN-04 | Listar ingredientes com busca e ordenação por nome/custo | M |
| IN-05 | Ao alterar o preço de compra, o sistema marca dependentes (preparos e fichas) como "recalcular" | M |

## Módulo 3 — Preparos (receitas base)

| ID | Requisito | Prior. |
|----|-----------|--------|
| PR-01 | Cadastrar preparo com nome (ex.: "Massa broto"), rendimento e unidade do rendimento | M |
| PR-02 | Adicionar itens ao preparo referenciando **ingrediente** ou **outro preparo** (massa dentro da ficha), com quantidade | M |
| PR-03 | O sistema calcula o custo total do preparo e o **custo por unidade de rendimento** | M |
| PR-04 | Preparos aninhados são resolvidos recursivamente (com detecção de ciclos) | M |
| PR-05 | Ao mudar custo de ingrediente, preparos dependentes recalculam | M |

## Módulo 4 — Custos fixos

| ID | Requisito | Prior. |
|----|-----------|--------|
| CF-01 | Cadastrar custo fixo com descrição, valor mensal e método de rateio (`PorPizza` ou `PercentualFaturamento`) | M |
| CF-02 | No rateio `PorPizza`, o usuário informa a **previsão de pizzas por mês**; o sistema deriva o custo indireto por pizza | M |
| CF-03 | Listar, editar e excluir custos fixos | M |
| CF-04 | O total de custos fixos e o rateio por pizza aparecem no dashboard | M |

## Módulo 5 — Taxas e configuração de precificação

| ID | Requisito | Prior. |
|----|-----------|--------|
| TX-01 | Configurar % de taxa de cartão | M |
| TX-02 | Configurar % de taxa de delivery e valor fixo de entrega (quando aplicável) | M |
| TX-03 | Configurar **margem desejada padrão** (% sobre o preço de venda) | M |
| TX-04 | A margem desejada pode ser sobrescrita por ficha técnica | M |

## Módulo 6 — Produtos e tamanhos

| ID | Requisito | Prior. |
|----|-----------|--------|
| PD-01 | Cadastrar produto (sabor) com categoria: `Pizza`, `Bebida` ou `Outro` | M |
| PD-02 | Cadastrar tamanhos (ex.: Broto 20cm, Média 30cm, Grande 35cm, Gigante 45cm) | M |
| PD-03 | Bebidas não têm ficha técnica por tamanho; têm preço de venda direto e custo de aquisição | M |

## Módulo 7 — Fichas técnicas

| ID | Requisito | Prior. |
|----|-----------|--------|
| FT-01 | Criar ficha técnica por combinação **produto × tamanho** | M |
| FT-02 | A ficha lista itens: ingrediente ou preparo, com quantidade e custo calculado | M |
| FT-03 | A ficha inclui custo de embalagem e % de perda adicional (opcional) | M |
| FT-04 | O sistema exibe ao vivo: custo total, preço sugerido, lucro estimado, margem % e status | M |
| FT-05 | O mesmo sabor pode ter fichas diferentes por tamanho | M |
| FT-06 | Excluir ficha bloqueada se referenciada em combo | C |

## Módulo 8 — Precificação (motor de cálculo)

| ID | Requisito | Prior. |
|----|-----------|--------|
| PC-01 | Calcular custo direto da ficha (ingredientes + preparos + embalagem + perdas) | M |
| PC-02 | Aplicar rateio de custos fixos ao custo indireto | M |
| PC-03 | Calcular preço sugerido por margem sobre **preço de venda**, descontando taxas | M |
| PC-04 | Calcular lucro estimado e margem % reais | M |
| PC-05 | Definir status `Dentro da margem` / `Precisa ajustar` comparando margem real × margem alvo | M |
| PC-06 | Recalcular todas as fichas em cadeia quando custos mudam e gerar alertas | M |

## Módulo 9 — Pizzas mistas

| ID | Requisito | Prior. |
|----|-----------|--------|
| MS-01 | Precificar meio a meio (2 sabores) do mesmo tamanho, com custo médio 50/50 | M |
| MS-02 | Precificar pizza de quatro sabores (custo igualmente dividido entre os 4) | M |
| MS-03 | O preço sugerido da mista usa o **maior preço** entre os sabores participantes | M |
| MS-04 | A regra da mista (média × maior preço) é configurável | C |

## Módulo 10 — Combos e bebidas

| ID | Requisito | Prior. |
|----|-----------|--------|
| CB-01 | Montar combo: 1 ou mais pizzas (sabor+tamanho) + 1 ou mais bebidas | M |
| CB-02 | Calcular custo do combo = soma dos custos − desconto informado | M |
| CB-03 | Preço do combo: soma dos preços − desconto, ou preço fixo informado | M |
| CB-04 | Exibir margem e lucro do combo | M |
| CB-05 | Precificação de bebida isolada: custo de aquisição, markup, preço e margem | M |

## Módulo 11 — Dashboard

| ID | Requisito | Prior. |
|----|-----------|--------|
| DB-01 | Indicadores gerais: nº de itens precificados, itens no vermelho, custo fixo mensal, custo indireto por pizza, margem média | M |
| DB-02 | Ranking por lucro (maior → menor) | M |
| DB-03 | Ranking por custo (maior → menor) | M |
| DB-04 | Consulta individual: selecionar produto + tamanho e ver a composição completa do cálculo | M |
| DB-05 | Gráfico de barras: margem % por sabor | C |

## Módulo 12 — Alertas de reajuste

| ID | Requisito | Prior. |
|----|-----------|--------|
| AL-01 | Quando o custo sobe e o item fica abaixo da margem alvo, gerar alerta `MargemAbaixo` | M |
| AL-02 | Lista de alertas no dashboard, com filtros por tipo e data | M |
| AL-03 | O usuário pode marcar alerta como resolvido (após ajustar preço) | M |
| AL-04 | Alerta por e-mail (resumo semanal) | F |

## Módulo 13 — Estoque

| ID | Requisito | Prior. |
|----|-----------|--------|
| ES-01 | Registrar entrada de estoque com quantidade, custo unitário e data | M |
| ES-02 | Registrar saída (consumo manual ou baixa manual) | M |
| ES-03 | Consultar saldo por ingrediente | M |
| ES-04 | Definir **estoque mínimo** por ingrediente; saldo abaixo gera alerta `Reposicao` | M |
| ES-05 | Baixa automática de estoque a cada produção (status `Ready`), com estorno automático se cancelado após pronto | M |
| ES-06 | Relatório de custo real de estoque e lucro consolidado no período (`/lucro`) | M |

## Módulo 14 — Operação: Equipe, Pedidos, Produção, Site Público e Lucro Real (docs/10)

| ID | Requisito | Prior. |
|----|-----------|--------|
| OP-01 | Gestão de usuários do tenant com papéis `owner`, `atendente` e `pizzaiolo` (proteção do último `owner`) | M |
| OP-02 | Registro de pedidos (balcão, retirada, entrega) com pizzas de 1, 2 ou 4 sabores, combos, bebidas, desconto e `Idempotency-Key` | M |
| OP-03 | Impressão de comanda térmica não fiscal (`/pedidos/:id/ticket`) com `@media print` | M |
| OP-04 | Fila da produção kanban (`/producao`) com polling de 8s, receita resumida e alerta sonoro | M |
| OP-05 | Cardápio público anônimo (`/menu/:slug`), checkout (`/checkout/:slug`) e acompanhamento por código (`/acompanhar/:slug/:code`) | M |
| OP-06 | Relatório de lucro real (`/lucro`) com faturamento, custo de insumos, taxa estimada de cartão (P8) e rateio de custos fixos (P9) | M |

## Regras de exclusão (valores globais)

- **RF-01**: excluir ingrediente em uso → 409 com mensagem listando onde é usado.
- **RF-02**: excluir preparo em uso → 409 idem.
- **RF-03**: excluir produto com ficha técnica → exigir exclusão em cascata explícita confirmada.
- **RF-04**: exclusões são **físicas** no MVP; histórico de preço de compra fica por conta do estoque (entrada com custo unitário).
