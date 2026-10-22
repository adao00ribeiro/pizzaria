using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PizzariaLucrativa.Api.Controllers.Shared;
using PizzariaLucrativa.Application.Interfaces;

namespace PizzariaLucrativa.Api.Controllers;

/// <summary>
/// Gestão de usuários e papéis da pizzaria (docs/10, Sprint 1) — somente owner.
/// </summary>
[Authorize(Policy = "OwnerOnly")]
[ApiVersion("1.0")]
public sealed class UsersController : ApiControllerBase
{
    private readonly IUserAdminService _users;

    public UsersController(IUserAdminService users) => _users = users;

    /// <summary>Lista os usuários do tenant logado.</summary>
    [HttpGet]
    public Task<IReadOnlyList<UserDto>> List(CancellationToken ct = default)
        => _users.ListAsync(ct);

    /// <summary>Papéis disponíveis (owner / atendente / pizzaiolo).</summary>
    [HttpGet("roles")]
    public Task<IReadOnlyList<string>> Roles(CancellationToken ct = default)
        => _users.RolesAsync(ct);

    /// <summary>Cria um usuário com papel no mesmo tenant.</summary>
    [HttpPost]
    public Task<UserDto> Create(CreateUserInput input, CancellationToken ct = default)
        => _users.CreateAsync(input, ct);

    /// <summary>Atualiza nome e/ou papel (último owner é protegido).</summary>
    [HttpPut("{id:guid}")]
    public Task<UserDto> Update(Guid id, UpdateUserInput input, CancellationToken ct = default)
        => _users.UpdateAsync(id, input, ct);

    /// <summary>Remove o usuário (o último owner não pode ser removido).</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        await _users.DeleteAsync(id, ct);
        return NoContent();
    }
}
