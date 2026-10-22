namespace PizzariaLucrativa.Application.Interfaces;

/// <summary>
/// Usuário autenticado do request (NF-S03). Necessário para gravar quem lançou o
/// pedido (<c>Order.PlacedByUserId</c>) sem a camada Application conhecer ASP.NET.
/// </summary>
public interface ICurrentUser
{
    /// <summary>Id do usuário do JWT; null em rotas anônimas (pedido pelo site público).</summary>
    string? UserId { get; }
}
