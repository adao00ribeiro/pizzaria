namespace PizzariaLucrativa.Domain.Enums;

public enum MarginStatus
{
    /// <summary>Margem real atinge (ou supera) a margem alvo.</summary>
    Within = 0,

    /// <summary>Margem real ficou abaixo da margem alvo — precisa ajustar o preço.</summary>
    NeedsAdjustment = 1
}
