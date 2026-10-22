using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace PizzariaLucrativa.Infrastructure.Persistence;

/// <summary>
/// Garante os papéis owner/atendente/pizzaiolo/garcom no startup (idempotente — docs/10, Sprint 1).
/// </summary>
public sealed class RoleSeeder
{
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ILogger<RoleSeeder> _logger;

    public RoleSeeder(RoleManager<IdentityRole> roleManager, ILogger<RoleSeeder> logger)
    {
        _roleManager = roleManager;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        foreach (var role in AppRoles.All)
        {
            if (await _roleManager.RoleExistsAsync(role))
                continue;

            var result = await _roleManager.CreateAsync(new IdentityRole(role));
            if (!result.Succeeded)
            {
                _logger.LogWarning(
                    "Falha ao criar o papel {Role}: {Errors}",
                    role, string.Join("; ", result.Errors.Select(e => e.Description)));
                continue;
            }

            _logger.LogInformation("Papel {Role} criado.", role);
        }
    }
}
