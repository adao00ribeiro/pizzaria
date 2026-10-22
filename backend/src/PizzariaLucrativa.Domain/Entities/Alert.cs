using PizzariaLucrativa.Domain.Enums;

namespace PizzariaLucrativa.Domain.Entities;

/// <summary>
/// Alerta gerado pelo recálculo em cadeia (margem abaixo do alvo) ou pelo estoque
/// (reposição). A unicidade de alerta aberto por (tipo, item) é garantida no serviço.
/// </summary>
public class Alert : TenantEntity
{
    public AlertType Type { get; set; }

    public Guid? TechnicalSheetId { get; set; }
    public TechnicalSheet? TechnicalSheet { get; set; }

    public Guid? IngredientId { get; set; }
    public Ingredient? Ingredient { get; set; }

    public string Message { get; set; } = string.Empty;

    public AlertStatus Status { get; set; } = AlertStatus.Open;

    public DateTime? ResolvedAt { get; set; }
}
