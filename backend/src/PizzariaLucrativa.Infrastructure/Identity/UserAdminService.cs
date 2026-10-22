using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PizzariaLucrativa.Application.Exceptions;
using PizzariaLucrativa.Application.Interfaces;
using PizzariaLucrativa.Infrastructure.Persistence;

namespace PizzariaLucrativa.Infrastructure.Identity;

/// <summary>
/// CRUD de usuários do tenant com validação de papel e proteção do último owner
/// (docs/10, Sprint 1). Sempre opera dentro do tenant do JWT (NF-S04).
/// </summary>
public sealed class UserAdminService : IUserAdminService
{
    private readonly UserManager<AppUser> _userManager;
    private readonly AppDbContext _db;
    private readonly ITenantProvider _tenants;

    public UserAdminService(
        UserManager<AppUser> userManager,
        AppDbContext db,
        ITenantProvider tenants)
    {
        _userManager = userManager;
        _db = db;
        _tenants = tenants;
    }

    public async Task<IReadOnlyList<UserDto>> ListAsync(CancellationToken ct = default)
    {
        var tenantId = _tenants.GetTenantId();
        var users = await _userManager.Users
            .Where(u => u.TenantId == tenantId)
            .OrderBy(u => u.FullName)
            .ToListAsync(ct);

        var result = new List<UserDto>(users.Count);
        foreach (var user in users)
            result.Add(await ToDtoAsync(user, ct));

        return result;
    }

    public async Task<UserDto> CreateAsync(CreateUserInput input, CancellationToken ct = default)
    {
        var role = AppRoles.Normalize(input.Role);
        if (role is null)
            throw new ValidationException("role", $"Papel inválido. Use {string.Join(", ", AppRoles.All)}.");

        var email = input.Email?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(email))
            throw new ValidationException("email", "Informe o e-mail.");
        if (await _userManager.FindByEmailAsync(email) is not null)
            throw new ConflictException("E-mail já cadastrado.");
        if (string.IsNullOrWhiteSpace(input.FullName))
            throw new ValidationException("fullName", "Informe o nome completo.");

        var user = new AppUser
        {
            UserName = email,
            Email = email,
            FullName = input.FullName.Trim(),
            TenantId = _tenants.GetTenantId()
        };

        var create = await _userManager.CreateAsync(user, input.Password);
        if (!create.Succeeded)
            throw new ValidationException(
                create.Errors.Select(e => ("password", e.Description)).ToList());

        var addToRole = await _userManager.AddToRoleAsync(user, role);
        if (!addToRole.Succeeded)
        {
            // Não deixa usuário órfão sem papel.
            await _userManager.DeleteAsync(user);
            throw new ValidationException(
                addToRole.Errors.Select(e => ("role", e.Description)).ToList());
        }

        return await ToDtoAsync(user, ct);
    }

    public async Task<UserDto> UpdateAsync(Guid id, UpdateUserInput input, CancellationToken ct = default)
    {
        var user = await FindAsync(id, ct);

        if (!string.IsNullOrWhiteSpace(input.FullName))
            user.FullName = input.FullName.Trim();

        if (input.Role is not null)
        {
            var role = AppRoles.Normalize(input.Role)
                       ?? throw new ValidationException("role", $"Papel inválido. Use {string.Join(", ", AppRoles.All)}.");

            var currentRoles = await _userManager.GetRolesAsync(user);
            if (!currentRoles.Contains(role))
            {
                // Não permite rebaixar/derrubar o último owner do tenant.
                if (currentRoles.Contains(AppRoles.Owner)
                    && !string.Equals(role, AppRoles.Owner, StringComparison.OrdinalIgnoreCase)
                    && await IsLastOwnerAsync(user, ct))
                    throw new ConflictException(
                        "Não é possível alterar o papel do último owner da pizzaria.");

                if (currentRoles.Count > 0)
                    await _userManager.RemoveFromRolesAsync(user, currentRoles);

                var result = await _userManager.AddToRoleAsync(user, role);
                if (!result.Succeeded)
                    throw new ValidationException(
                        result.Errors.Select(e => ("role", e.Description)).ToList());
            }
        }

        var update = await _userManager.UpdateAsync(user);
        if (!update.Succeeded)
            throw new ValidationException(
                update.Errors.Select(e => ("user", e.Description)).ToList());

        return await ToDtoAsync(user, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var user = await FindAsync(id, ct);
        var roles = await _userManager.GetRolesAsync(user);

        if (roles.Contains(AppRoles.Owner) && await IsLastOwnerAsync(user, ct))
            throw new ConflictException("Não é possível remover o último owner da pizzaria.");

        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
            throw new ValidationException(
                result.Errors.Select(e => ("user", e.Description)).ToList());
    }

    public Task<IReadOnlyList<string>> RolesAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<string>>(AppRoles.All);

    // ----------------------------------------------------------------- helpers

    private async Task<AppUser> FindAsync(Guid id, CancellationToken ct)
    {
        var tenantId = _tenants.GetTenantId();
        var user = await _userManager.Users
            .FirstOrDefaultAsync(u => u.Id == id.ToString() && u.TenantId == tenantId, ct);

        // 404 para inexistente OU de outro tenant (sem revelar existência — NF-S04).
        return user ?? throw new NotFoundException("Usuário não encontrado.");
    }

    private async Task<bool> IsLastOwnerAsync(AppUser current, CancellationToken ct)
    {
        var tenantId = _tenants.GetTenantId();
        var owners = await _userManager.Users
            .Where(u => u.TenantId == tenantId)
            .ToListAsync(ct);

        var ownerCount = 0;
        foreach (var owner in owners)
        {
            if (await _userManager.IsInRoleAsync(owner, AppRoles.Owner))
                ownerCount++;
        }

        return ownerCount <= 1;
    }

    private async Task<UserDto> ToDtoAsync(AppUser user, CancellationToken ct)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var role = roles.FirstOrDefault() ?? AppRoles.Owner;
        return new UserDto(
            Guid.Parse(user.Id),
            user.Email ?? string.Empty,
            user.FullName,
            role,
            roles.Contains(AppRoles.Owner),
            user.CreatedAt);
    }
}
