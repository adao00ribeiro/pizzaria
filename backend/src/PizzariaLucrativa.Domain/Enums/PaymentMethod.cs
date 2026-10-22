namespace PizzariaLucrativa.Domain.Enums;

/// <summary>
/// Forma de pagamento informada no pedido (premissa P8). Opcional: usada só no
/// ticket/comanda e para estimar a taxa de cartão no relatório de lucro.
/// Não há gateway de pagamento.
/// </summary>
public enum PaymentMethod
{
    Cash = 0,
    Card = 1,
    Pix = 2
}
