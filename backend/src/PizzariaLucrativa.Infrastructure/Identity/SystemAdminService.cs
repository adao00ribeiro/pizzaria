using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using PizzariaLucrativa.Application.DTOs;
using PizzariaLucrativa.Application.Exceptions;
using PizzariaLucrativa.Application.Interfaces;
using PizzariaLucrativa.Domain.Entities;
using PizzariaLucrativa.Infrastructure.Persistence;
using AppValidationException = PizzariaLucrativa.Application.Exceptions.ValidationException;

namespace PizzariaLucrativa.Infrastructure.Identity;

public sealed class SystemAdminService : ISystemAdminService
{
    private readonly AppDbContext _db;
    private readonly ITokenService _tokens;
    private readonly IPasswordHasher<SystemAdmin> _passwordHasher;
    private readonly IMemoryCache _cache;
    private readonly ILogger<SystemAdminService> _logger;

    public SystemAdminService(
        AppDbContext db,
        ITokenService tokens,
        IPasswordHasher<SystemAdmin> passwordHasher,
        IMemoryCache cache,
        ILogger<SystemAdminService> logger)
    {
        _db = db;
        _tokens = tokens;
        _passwordHasher = passwordHasher;
        _cache = cache;
        _logger = logger;
    }

    public async Task<PagedResult<TenantListItemDto>> GetTenantsAsync(
        int page,
        int pageSize,
        string? search,
        bool? isActive,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 15;
        if (pageSize > 50) pageSize = 50;

        var tenantQuery = _db.Tenants.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            tenantQuery = tenantQuery.Where(t => t.Name.ToLower().Contains(term) || (t.Slug != null && t.Slug.ToLower().Contains(term)));
        }

        if (isActive.HasValue)
        {
            tenantQuery = tenantQuery.Where(t => t.IsActive == isActive.Value);
        }

        var total = await tenantQuery.CountAsync(ct);
        var tenants = await tenantQuery
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var tenantIds = tenants.Select(t => t.Id).ToList();
        var owners = await _db.Users.AsNoTracking()
            .Where(u => tenantIds.Contains(u.TenantId))
            .OrderBy(u => u.CreatedAt)
            .ToListAsync(ct);

        var ownerByTenant = owners
            .GroupBy(u => u.TenantId)
            .ToDictionary(g => g.Key, g => g.First());

        var dtos = tenants.Select(t =>
        {
            ownerByTenant.TryGetValue(t.Id, out var owner);
            return new TenantListItemDto(
                t.Id,
                t.Name,
                t.Slug,
                t.CreatedAt,
                owner?.FullName ?? "Sem proprietário",
                owner?.Email ?? "",
                t.IsActive,
                t.DeactivatedAt,
                t.DeactivationReason
            );
        }).ToList();

        return new PagedResult<TenantListItemDto>(dtos, total);
    }

    public async Task<TenantListItemDto> UpdateTenantStatusAsync(
        Guid tenantId,
        TenantStatusUpdateInput input,
        CancellationToken ct = default)
    {
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct);
        if (tenant is null)
            throw new NotFoundException("Pizzaria não encontrada.");

        if (input.IsActive)
        {
            tenant.IsActive = true;
            tenant.DeactivatedAt = null;
            tenant.DeactivationReason = null;
        }
        else
        {
            tenant.IsActive = false;
            tenant.DeactivatedAt = DateTime.UtcNow;
            tenant.DeactivationReason = input.Reason?.Trim();
        }

        tenant.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        // Invalida cache de status e slug
        _cache.Remove($"tenant_active_{tenantId}");
        if (!string.IsNullOrWhiteSpace(tenant.Slug))
        {
            _cache.Remove($"tenant_slug_{tenant.Slug.ToLowerInvariant()}");
        }

        var owner = await _db.Users.AsNoTracking()
            .Where(u => u.TenantId == tenant.Id)
            .OrderBy(u => u.CreatedAt)
            .FirstOrDefaultAsync(ct);

        return new TenantListItemDto(
            tenant.Id,
            tenant.Name,
            tenant.Slug,
            tenant.CreatedAt,
            owner?.FullName ?? "Sem proprietário",
            owner?.Email ?? "",
            tenant.IsActive,
            tenant.DeactivatedAt,
            tenant.DeactivationReason
        );
    }

    public async Task<SystemAdminAuthResultDto> LoginAsync(
        SystemAdminLoginInput input,
        CancellationToken ct = default)
    {
        var normalizedEmail = input.Email.Trim().ToLowerInvariant();
        var admin = await _db.SystemAdmins.FirstOrDefaultAsync(a => a.Email.ToLower() == normalizedEmail, ct);
        if (admin is null)
            throw new UnauthorizedException("Credenciais inválidas.");

        var verification = _passwordHasher.VerifyHashedPassword(admin, admin.PasswordHash, input.Password);
        if (verification == PasswordVerificationResult.Failed)
            throw new UnauthorizedException("Credenciais inválidas.");

        admin.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var (token, expiresAt) = _tokens.CreateSystemAdminToken(admin);
        return new SystemAdminAuthResultDto(token, expiresAt, admin.Email, admin.FullName);
    }

    public async Task ForgotPasswordAsync(
        ForgotPasswordInput input,
        CancellationToken ct = default)
    {
        var normalizedEmail = input.Email.Trim().ToLowerInvariant();
        var admin = await _db.SystemAdmins.FirstOrDefaultAsync(a => a.Email.ToLower() == normalizedEmail, ct);
        if (admin is not null)
        {
            var rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken))).ToLowerInvariant();
            admin.PasswordResetTokenHash = hash;
            admin.PasswordResetTokenExpiresAt = DateTime.UtcNow.AddMinutes(15);
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Password reset token for SystemAdmin {Email}: {Token} (Expires: {Expires})",
                admin.Email, rawToken, admin.PasswordResetTokenExpiresAt);
        }
    }

    public async Task ResetPasswordAsync(
        ResetPasswordInput input,
        CancellationToken ct = default)
    {
        var rawToken = input.Token.Trim();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken))).ToLowerInvariant();

        var admin = await _db.SystemAdmins.FirstOrDefaultAsync(
            a => a.PasswordResetTokenHash == hash && a.PasswordResetTokenExpiresAt > DateTime.UtcNow, ct);

        if (admin is null)
            throw new AppValidationException("token", "Token de recuperação inválido ou expirado.");

        admin.PasswordHash = _passwordHasher.HashPassword(admin, input.NewPassword);
        admin.PasswordResetTokenHash = null;
        admin.PasswordResetTokenExpiresAt = null;
        admin.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
    }

    public async Task ResetPasswordCliAsync(
        string email,
        string newPassword,
        CancellationToken ct = default)
    {
        var admin = await _db.SystemAdmins.OrderBy(a => a.CreatedAt).FirstOrDefaultAsync(ct);
        if (admin is null)
        {
            admin = new SystemAdmin
            {
                Id = SystemAdmin.SingletonId,
                Email = email.Trim().ToLowerInvariant(),
                FullName = "Administrador do Sistema",
                CreatedAt = DateTime.UtcNow
            };
            admin.PasswordHash = _passwordHasher.HashPassword(admin, newPassword);
            _db.SystemAdmins.Add(admin);
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(email))
                admin.Email = email.Trim().ToLowerInvariant();

            admin.PasswordHash = _passwordHasher.HashPassword(admin, newPassword);
            admin.PasswordResetTokenHash = null;
            admin.PasswordResetTokenExpiresAt = null;
            admin.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
    }
}
