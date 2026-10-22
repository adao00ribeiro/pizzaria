using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PizzariaLucrativa.Application.Exceptions;
using PizzariaLucrativa.Application.Interfaces;
using PizzariaLucrativa.Domain.Entities;
using PizzariaLucrativa.Domain.Services;
using PizzariaLucrativa.Infrastructure.Persistence;

namespace PizzariaLucrativa.Infrastructure.Identity;

/// <summary>
/// Register/login/refresh com ASP.NET Identity + JWT (NF-T01):
/// access token de 15 min, refresh token de 7 dias armazenado como hash SHA-256 e
/// rotacionado a cada uso (o anterior é revogado).
/// </summary>
public sealed class AuthService : IAuthService
{
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(7);

    private readonly UserManager<AppUser> _userManager;
    private readonly AppDbContext _db;
    private readonly ITokenService _tokens;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        UserManager<AppUser> userManager,
        AppDbContext db,
        ITokenService tokens,
        ILogger<AuthService> logger)
    {
        _userManager = userManager;
        _db = db;
        _tokens = tokens;
        _logger = logger;
    }

    public async Task<AuthResult> RegisterAsync(RegisterCommand command, CancellationToken ct = default)
    {
        var normalizedEmail = command.Email.Trim();

        if (await _userManager.FindByEmailAsync(normalizedEmail) is not null)
            throw new ConflictException("E-mail já cadastrado.");

        // 1) Tenant primeiro — é a raiz do isolamento (NF-S03).
        //    O slug público nasce do nome e já é único (docs/10, Sprint 1).
        var tenant = new Tenant
        {
            Name = command.PizzeriaName.Trim(),
            Slug = await GenerateUniqueSlugAsync(command.PizzeriaName, ct)
        };
        _db.Add(tenant);
        await _db.SaveChangesAsync(ct);

        // 2) Usuário vinculado ao tenant.
        var user = new AppUser
        {
            UserName = normalizedEmail,
            Email = normalizedEmail,
            FullName = command.FullName.Trim(),
            TenantId = tenant.Id
        };

        var result = await _userManager.CreateAsync(user, command.Password);
        if (!result.Succeeded)
        {
            // Desfaz o tenant criado para não deixar registro órfão.
            _db.Remove(tenant);
            await _db.SaveChangesAsync(ct);
            throw new ValidationException(
                result.Errors.Select(e => ("password", e.Description)).ToList());
        }

        // Quem registra a pizzaria é o owner (docs/10, premissa P4).
        await _userManager.AddToRoleAsync(user, AppRoles.Owner);

        // 3) Configuração de taxa padrão do tenant (margem 60%, cartão 2,99%).
        _db.Add(new FeeConfig
        {
            TenantId = tenant.Id,
            CardFeePercentage = 2.99m,
            DefaultMarginPercentage = 60m
        });
        await _db.SaveChangesAsync(ct);

        return await IssueTokensAsync(user, ct);
    }

    public async Task<AuthResult> LoginAsync(LoginCommand command, CancellationToken ct = default)
    {
        var user = await _userManager.FindByEmailAsync(command.Email.Trim());
        if (user is null || !await _userManager.CheckPasswordAsync(user, command.Password))
            throw new UnauthorizedException("E-mail ou senha inválidos.");

        return await IssueTokensAsync(user, ct);
    }

    public async Task<AuthResult> RefreshAsync(RefreshCommand command, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command.RefreshToken))
            throw new UnauthorizedException("Refresh token inválido.");

        var hash = HashToken(command.RefreshToken);
        var stored = await _db.Set<RefreshToken>()
            .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (stored is null || stored.RevokedAt is not null || stored.ExpiresAt <= DateTime.UtcNow)
            throw new UnauthorizedException("Refresh token inválido ou expirado.");

        var user = await _userManager.FindByIdAsync(stored.UserId);
        if (user is null)
            throw new UnauthorizedException("Refresh token inválido ou expirado.");

        // Rotação: revoga o token usado e emite um novo par (NF-T01).
        stored.RevokedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return await IssueTokensAsync(user, ct);
    }

    // ----------------------------------------------------------------- helpers

    private async Task<AuthResult> IssueTokensAsync(AppUser user, CancellationToken ct)
    {
        var (accessToken, expiresIn) = await _tokens.CreateAccessTokenAsync(user);

        var rawRefreshToken = GenerateRefreshToken();
        var refresh = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = HashToken(rawRefreshToken),
            ExpiresAt = DateTime.UtcNow.Add(RefreshTokenLifetime)
        };

        _db.Add(refresh);
        await _db.SaveChangesAsync(ct);

        return new AuthResult(accessToken, rawRefreshToken, expiresIn);
    }

    private static string GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }

    private static string HashToken(string token)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(digest).ToLowerInvariant();
    }

    private async Task<string?> GenerateUniqueSlugAsync(string name, CancellationToken ct)
    {
        var baseSlug = SlugGenerator.Generate(name);
        if (string.IsNullOrWhiteSpace(baseSlug))
        {
            baseSlug = "pizzaria";
        }

        var current = baseSlug;
        var suffix = 2;
        while (await SlugExistsAsync(current, ct))
        {
            current = SlugGenerator.WithSuffix(baseSlug, suffix);
            suffix++;

            if (suffix > 100)
            {
                _logger.LogWarning("Não foi possível gerar slug único para {Name}", name);
                return baseSlug;
            }
        }

        return current;
    }

    private async Task<bool> SlugExistsAsync(string slug, CancellationToken ct)
    {
        return await _db.Set<Tenant>()
            .AnyAsync(t => t.Slug != null && t.Slug.ToLower() == slug.ToLower(), ct);
    }
}
