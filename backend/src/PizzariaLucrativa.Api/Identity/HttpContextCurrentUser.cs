using System.Security.Claims;
using PizzariaLucrativa.Application.Interfaces;

namespace PizzariaLucrativa.Api.Identity;

/// <summary>
/// Lê o usuário autenticado do <see cref="HttpContext"/> (docs/10, Sprint 2).
/// O JwtBearer mapeia a claim <c>sub</c> para <see cref="ClaimTypes.NameIdentifier"/>,
/// mas a claim crua é considerada como fallback.
/// </summary>
public sealed class HttpContextCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;

    public HttpContextCurrentUser(IHttpContextAccessor accessor) => _accessor = accessor;

    public string? UserId
    {
        get
        {
            var user = _accessor.HttpContext?.User;
            if (user is null)
                return null;

            return user.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? user.FindFirstValue("sub");
        }
    }
}
