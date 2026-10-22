# 03 — Requisitos Não Funcionais

## 1. Segurança

| ID | Requisito |
|----|-----------|
| NF-S01 | Autenticação por JWT; access token com validade de 15 min e refresh token de 7 dias, rotacionado a cada uso |
| NF-S02 | Senhas comhash via ASP.NET Identity (PBKDF2), com política: mínimo 8 caracteres, 1 maiúscula, 1 número |
| NF-S03 | **Isolamento multi-tenant**: query filter global no EF Core (`HasQueryFilter(e => e.TenantId == currentTenant)`) + validação no handler de autorização |
| NF-S04 | Nenhum endpoint aceita `TenantId` vindo do cliente — ele é sempre resolvido do token |
| NF-S05 | HTTPS obrigatório; headers de segurança (HSTS, X-Content-Type-Options, X-Frame-Options) |
| NF-S06 | Rate limiting no login (máx. 5 tentativas/min por IP) |
| NF-S07 | Validação de entrada server-side em todos os endpoints (FluentValidation ou DataAnnotations) |
| NF-S08 | CORS restrito ao domínio do frontend configurável por variável de ambiente |

## 2. Confiabilidade e integridade de dados

| ID | Requisito |
|----|-----------|
| NF-R01 | O **motor de cálculo é função pura de domínio** (sem EF), coberta por testes unitários com casos de referência fixos |
| NF-R02 | Recálculo em cadeia (ingrediente → preparo → ficha → mista → combo) roda de forma idempotente |
| NF-R03 | Operações de recálculo usam transação; falha parcial não deixa base inconsistente |
| NF-R04 | Detecção de ciclos em preparos aninhados (erro claro, não stack overflow) |
| NF-R05 | Valores monetários armazenados em `decimal(18,4)`; nunca `double` |
| NF-R06 | Exibição monetária arredondada para 2 casas, mas cálculos com 4 casas internamente |
| NF-R07 | Backup: rotina documentada de dump do banco (diária) |

## 3. Performance

| ID | Requisito |
|----|-----------|
| NF-P01 | Cálculo de uma ficha técnica: resposta em < 200 ms |
| NF-P02 | Dashboard: resposta em < 1 s para tenants com até 500 fichas |
| NF-P03 | Recálculo em cadeia completo: execução assíncrona em background (fila em memória / `BackgroundService`), com aviso "recalculando" na UI |
| NF-P04 | Resultados de cálculo cacheados por versão de custo (invalidação por `CostVersion` incrementado ao mudar insumo) |
| NF-P05 | Listagens paginadas (server-side) com página de 20 itens padrão |

## 4. Usabilidade

| ID | Requisito |
|----|-----------|
| NF-U01 | Interface 100% em português, com termos de negócio (não técnicos) |
| NF-U02 | Responsiva: uso no celular do dono (Material breakpoints) |
| NF-U03 | Formulários com validação inline e mensagens claras em português |
| NF-U04 | Montador de ficha técnica com busca/autocomplete de insumos e cálculo ao vivo enquanto digita |
| NF-U05 | Status visual (chip verde/vermelho) em todas as listas de precificação |
| NF-U06 | Feedback via snackbar em CRUD (salvo, erro, excluído) |
| NF-U07 | Tema claro com paleta da marca (vermelho/branco/ouro), acessível (contraste AA) |

## 5. Atribuição e arquitetura

| ID | Requisito |
|----|-----------|
| NF-A01 | Backend em camadas: `Api` → `Application` → `Domain` → `Infrastructure` (clean architecture leve) |
| NF-A02 | `Domain` sem dependência de framework (puras entidades + serviços de cálculo) |
| NF-A03 | API versionada no path (`/api/v1/...`) |
| NF-A04 | OpenAPI (Swagger) habilitado em ambiente de desenvolvimento |
| NF-A05 | Logs estruturados (Serilog) com `TenantId` e `UserId` em contexto |

## 6. Operação e implantação

| ID | Requisito |
|----|-----------|
| NF-O01 | Backend containerizável (Dockerfile multi-stage) |
| NF-O02 | Frontend buildado como estáticos servidos por Nginx ou CDN |
| NF-O03 | Configuração por variáveis de ambiente (connection string, JWT secret, origem CORS) |
| NF-O04 | Migrações do EF Core aplicadas via `dotnet ef` no pipeline de deploy |

## 7. Testes

| ID | Requisito |
|----|-----------|
| NF-T01 | Testes unitários da engine de cálculo com ao menos 15 casos de referência (golden files) |
| NF-T02 | Testes de integração dos endpoints CRUD + auth (WebApplicationFactory) |
| NF-T03 | Teste automatizado de isolamento de tenant (usuário A não lê dados de B) |
| NF-T04 | Cobertura mínima da camada `Domain`: 80% |
