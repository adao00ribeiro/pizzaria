---
name: api-controller-pattern
description: Standard pattern and Clean Architecture rules for creating or modifying ASP.NET Core 8 REST API Controllers in `backend/src/PizzariaLucrativa.Api/Controllers/`. Activate this skill whenever creating, refactoring, or auditing API controllers, routes, and authorization policies.
---

# Padrão de Controllers da API (`PizzariaLucrativa.Api`)

Esta skill define o padrão arquitetural obrigatório para todos os Controllers REST localizados em `backend/src/PizzariaLucrativa.Api/Controllers/`.

---

## 1. Classe Base Obrigatória (`ApiControllerBase`) e Versionamento (`Asp.Versioning`)

Todo Controller em `backend/src/PizzariaLucrativa.Api/Controllers/` **DEVE**:
1. Ser `sealed` e herdar de `ApiControllerBase` (`using PizzariaLucrativa.Api.Controllers.Shared;`), e **NUNCA** diretamente de `ControllerBase` ou `Controller`.
2. Declarar o atributo `[ApiVersion("1.0")]` do pacote oficial `Asp.Versioning.Mvc` (incluso via `<Using Include="Asp.Versioning" />` no `.csproj`).

### Código de Referência: `backend/src/PizzariaLucrativa.Api/Controllers/Shared/ApiControllerBase.cs`

```csharp
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;

namespace PizzariaLucrativa.Api.Controllers.Shared;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public abstract class ApiControllerBase : ControllerBase
{
}
```

### Configuração do Versionamento (`Asp.Versioning.Mvc`)
Em `backend/src/PizzariaLucrativa.Api/Extensions/VersioningExtensions.cs`, o método `AddApiVersioningConfiguration()` registra o versionamento por segmento de URL com `UrlSegmentApiVersionReader` e a integração com o ApiExplorer:

```csharp
services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
})
.AddMvc()
.AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});
```

Isso permite que todas as rotas baseadas em `api/v{version:apiVersion}/...` atendam `/api/v1/...` com suporte oficial a versionamento.

---

## 2. Regras de Roteamento

1. **Controllers Padrão (Rota Automática por Convenção)**:
   - **NÃO** repita `[ApiController]` na classe derivada (já é herdado de `ApiControllerBase`).
   - **NÃO** declare `[Route("api/v1/...")]` ou `[Route("api/v{version:apiversion}/[controller]")]` quando o sufixo da rota for o próprio nome do controller (ex.: `IngredientsController` -> `/api/v1/ingredients`, `OrdersController` -> `/api/v1/orders`).
2. **Controllers com Rota Customizada (Override de `[Route]`)**:
   - Quando a rota exigir um caminho específico diferente de `[controller]`, sobrescreva apenas o atributo `[Route(...)]` usando sempre o prefixo `api/v{version:apiversion}/...` (nunca hardcode `api/v1/...`).
   - Exemplos reais do projeto:
     - `PublicController`: `[Route("api/v{version:apiversion}/public/{slug}")]`
     - `ReportsController`: `[Route("api/v{version:apiversion}/reports/profit")]`

---

## 3. Regras de Thin Controller (Clean Architecture & DIP)

1. **Dependências Permitidas**:
   - Um Controller só pode injetar interfaces da camada `Application` (`using PizzariaLucrativa.Application.Interfaces;`) e receber/retornar DTOs (`using PizzariaLucrativa.Application.DTOs;`).
2. **Dependências Proibidas**:
   - **PROIBIDO** injetar `AppDbContext`, `IAppDbContext`, repositórios de infraestrutura, `UserManager<AppUser>` ou implementações concretas de serviços.
   - **PROIBIDO** escrever lógica de negócio, cálculos financeiros, queries LINQ/EF Core ou validações manuais de domínio dentro de actions.
3. **Validação e Erros Automáticos**:
   - O `ValidationFilter` global é registrado em `AddApiControllers()` e valida automaticamente os DTOs de entrada com FluentValidation antes da action executar.
   - Exceções (`DomainException`, `NotFoundException`, `ConflictException`, `AppValidationException`) são traduzidas automaticamente pelo `ExceptionHandlingMiddleware` — **não** use blocos `try/catch` nos controllers.
4. **Propagação de `CancellationToken`**:
   - Sempre receba `CancellationToken ct = default` nas actions assíncronas e repasse-o para o método do serviço (`_service.MetodoAsync(..., ct)`).

---

## 4. Políticas de Autorização (RBAC) e Rate Limiting

Todo controller ou action deve declarar explicitamente sua política de acesso (configuradas em `ApiServiceCollectionExtensions.AddApiAuthorizationPolicies()`):

| Atributo | Quando usar | Exemplo de Controller |
|---|---|---|
| `[Authorize]` | Endpoints autenticados gerais do tenant | `IngredientsController`, `ProductsController`, `PreparationsController` |
| `[Authorize(Policy = "OwnerOnly")]` | Recursos exclusivos do proprietário (`owner`), como gestão de equipe e relatório de lucro real | `UsersController`, `ReportsController` |
| `[Authorize(Policy = "AtendenteOuOwner")]` | Operação de atendimento/PDV (`atendente` ou `owner`) | `OrdersController` |
| `[Authorize(Policy = "ProducaoOuOwner")]` | Fila kanban da cozinha e estoque (`pizzaiolo` ou `owner`) | `KitchenController`, `StockController` |
| `[AllowAnonymous]` | Endpoints públicos sem autenticação JWT (login/registro e cardápio público) | `AuthController`, `PublicController` |

Para endpoints públicos sensíveis, combine com `[EnableRateLimiting("login")]`, `[EnableRateLimiting("public-orders")]` ou `[EnableRateLimiting("public-tracking")]`.

---

## 5. Templates de Referência Reais

### Exemplo 1: Controller CRUD Padrão (`IngredientsController.cs`)

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PizzariaLucrativa.Api.Controllers.Shared;
using PizzariaLucrativa.Application.DTOs;
using PizzariaLucrativa.Application.Interfaces;

namespace PizzariaLucrativa.Api.Controllers;

/// <summary>Insumos comprados a granel — custo unitário derivado (docs/07 §2).</summary>
[Authorize]
[ApiVersion("1.0")]
public sealed class IngredientsController : ApiControllerBase
{
    private readonly IIngredientService _ingredients;

    public IngredientsController(IIngredientService ingredients) => _ingredients = ingredients;

    [HttpGet]
    public Task<PagedResult<IngredientDto>> List(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => _ingredients.ListAsync(search, new PagedRequest(page, pageSize), ct);

    [HttpGet("{id:guid}")]
    public Task<IngredientDto> Get(Guid id, CancellationToken ct = default)
        => _ingredients.GetAsync(id, ct);

    [HttpPost]
    public Task<IngredientDto> Create(IngredientInput input, CancellationToken ct = default)
        => _ingredients.CreateAsync(input, ct);

    [HttpPut("{id:guid}")]
    public Task<IngredientDto> Update(Guid id, IngredientInput input, CancellationToken ct = default)
        => _ingredients.UpdateAsync(id, input, ct);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        await _ingredients.DeleteAsync(id, ct);
        return NoContent();
    }
}
```

### Exemplo 2: Controller com Rota Customizada e Policy RBAC (`ReportsController.cs`)

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PizzariaLucrativa.Api.Controllers.Shared;
using PizzariaLucrativa.Application.DTOs;
using PizzariaLucrativa.Application.Interfaces;

namespace PizzariaLucrativa.Api.Controllers;

/// <summary>
/// Relatório de lucro real (docs/10, Sprint 6). Restrito exclusivamente ao <c>owner</c>.
/// </summary>
[Authorize(Policy = "OwnerOnly")]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/reports/profit")]
public sealed class ReportsController : ApiControllerBase
{
    private readonly IReportService _reports;

    public ReportsController(IReportService reports) => _reports = reports;

    [HttpGet]
    public Task<ProfitReportDto> Summary(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct = default)
        => _reports.GetProfitSummaryAsync(from, to, ct);
}
```

### Exemplo 3: Controller Público com Slug e Rate Limiting (`PublicController.cs`)

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PizzariaLucrativa.Api.Controllers.Shared;
using PizzariaLucrativa.Application.DTOs;
using PizzariaLucrativa.Application.Interfaces;

namespace PizzariaLucrativa.Api.Controllers;

[AllowAnonymous]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/public/{slug}")]
public sealed class PublicController : ApiControllerBase
{
    private readonly IOrderService _orders;

    public PublicController(IOrderService orders) => _orders = orders;

    [HttpGet("menu")]
    public Task<PublicMenuDto> Menu(CancellationToken ct = default)
        => _orders.GetCatalogAsync(ct);

    [HttpPost("orders")]
    [EnableRateLimiting("public-orders")]
    public Task<OrderDto> CreateOrder(
        CreatePublicOrderInput input,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey = null,
        CancellationToken ct = default)
        => _orders.CreatePublicAsync(input, ct, idempotencyKey);
}
```

---

## 6. Checklist de Revisão (QA & Code Review)

- [ ] A classe é `public sealed class <Nome>Controller : ApiControllerBase`?
- [ ] Possui o atributo `[ApiVersion("1.0")]` declarado?
- [ ] Importa `using PizzariaLucrativa.Api.Controllers.Shared;`?
- [ ] **Não** possui `[ApiController]` duplicado?
- [ ] **Não** possui `[Route("api/v1/...")]` hardcoded (usa a rota herdada ou `[Route("api/v{version:apiVersion}/...")]`)?
- [ ] Injeta apenas interfaces de `PizzariaLucrativa.Application.Interfaces`?
- [ ] Possui atributo explícito de autorização (`[Authorize]`, `[Authorize(Policy = "...")]` ou `[AllowAnonymous]`)?
- [ ] Propaga `CancellationToken ct = default` para os métodos assíncronos do serviço?
