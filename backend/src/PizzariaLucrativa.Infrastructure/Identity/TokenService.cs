using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using PizzariaLucrativa.Application.Interfaces;
using PizzariaLucrativa.Infrastructure.Persistence;

namespace PizzariaLucrativa.Infrastructure.Identity;

/// <summary>Opções JWT lidas da seção "Jwt" da configuração.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>Segredo HMAC-SHA256 (mínimo 32 caracteres).</summary>
    public string Key { get; set; } = string.Empty;

    public string Issuer { get; set; } = "PizzariaLucrativa";

    public string Audience { get; set; } = "PizzariaLucrativa";

    /// <summary>Validade do access token (NF-T01: 15 minutos).</summary>
    public int AccessTokenMinutes { get; set; } = 15;
}

public interface ITokenService
{
    /// <summary>
    /// Gera o access token JWT do usuário (claims: sub, email, name, tenant_id e uma
    /// claim <c>role</c> por papel do usuário — docs/10, Sprint 1).
    /// </summary>
    Task<(string AccessToken, int ExpiresIn)> CreateAccessTokenAsync(AppUser user);
}

/// <summary>Emissão de access token JWT HS256 com expiração curta (15 min).</summary>
public sealed class TokenService : ITokenService
{
    private readonly JwtOptions _options;
    private readonly UserManager<AppUser> _userManager;

    public TokenService(
        Microsoft.Extensions.Options.IOptions<JwtOptions> options,
        UserManager<AppUser> userManager)
    {
        _options = options.Value;
        _userManager = userManager;
        if (string.IsNullOrWhiteSpace(_options.Key) || _options.Key.Length < 32)
            throw new InvalidOperationException(
                "Jwt:Key ausente ou muito curto (mínimo de 32 caracteres). Configure a seção \"Jwt\".");
    }

    public async Task<(string AccessToken, int ExpiresIn)> CreateAccessTokenAsync(AppUser user)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(_options.AccessTokenMinutes);

        // Papel real do usuário (UserManager), não mais fixo em "owner" (docs/10, Sprint 1).
        var roles = await _userManager.GetRolesAsync(user);
        if (roles.Count == 0)
            roles = [AppRoles.Owner];

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Name, user.FullName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new("tenant_id", user.TenantId.ToString())
        };

        // Uma claim "role" por papel: o handler do JwtBearer mapeia todas para
        // ClaimTypes.Role (IsInRole/RequireRole funcionam com múltiplos papéis).
        foreach (var role in roles)
            claims.Add(new Claim("role", role));

        // Lista explícita para clientes que decodificam o token sem o mapeamento inbound.
        claims.Add(new Claim("role_list", string.Join(',', roles)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var jwt = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        var token = new JwtSecurityTokenHandler().WriteToken(jwt);
        // Contrato estável (docs/07): expiresIn em segundos cheios da validade, sem
        // depender do tempo decorrido na emissão.
        return (token, _options.AccessTokenMinutes * 60);
    }
}
