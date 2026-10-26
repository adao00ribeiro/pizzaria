namespace PizzariaLucrativa.Application.Options;

/// <summary>
/// Configurações do subsistema de IA (Google Gemini, histórico e orçamento).
/// Mapeia a seção "Ai" do appsettings.
/// </summary>
public sealed class AiOptions
{
    public const string SectionName = "Ai";

    public bool Enabled { get; set; } = false;
    public int RolloutPercentage { get; set; } = 100;
    public string Provider { get; set; } = "Gemini";
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com";
    public string Model { get; set; } = "gemini-1.5-flash";
    public string ApiKey { get; set; } = string.Empty;
    public int MaxOutputTokens { get; set; } = 1024;
    public int TimeoutSeconds { get; set; } = 25;
    public int MaxToolRounds { get; set; } = 4;
    public int MaxHistoryMessages { get; set; } = 12;
    public int CacheMinutes { get; set; } = 5;

    public AiBudgetOptions Budget { get; set; } = new();
    public AiHistoryOptions History { get; set; } = new();

    public void Validate()
    {
        if (!Enabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            throw new InvalidOperationException(
                "A chave da API do assistente (Ai:ApiKey) é obrigatória quando Ai:Enabled está ativo.");
        }

        if (!string.Equals(Provider, "Gemini", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Provedor de IA '{Provider}' não é suportado no MVP. Apenas 'Gemini' é aceito.");
        }

        if (RolloutPercentage < 0 || RolloutPercentage > 100)
        {
            throw new InvalidOperationException("Ai:RolloutPercentage deve estar entre 0 e 100.");
        }

        if (Budget.Enforce && Budget.MonthlyUsdPerTenant <= 0)
        {
            throw new InvalidOperationException(
                "Ai:Budget:MonthlyUsdPerTenant deve ser maior que zero quando o controle de orçamento (Enforce) está ativo.");
        }

        if (MaxToolRounds <= 0)
        {
            throw new InvalidOperationException("Ai:MaxToolRounds deve ser maior que zero.");
        }

        if (TimeoutSeconds <= 0)
        {
            throw new InvalidOperationException("Ai:TimeoutSeconds deve ser maior que zero.");
        }
    }
}

public sealed class AiBudgetOptions
{
    public decimal MonthlyUsdPerTenant { get; set; } = 5.00m;
    public bool Enforce { get; set; } = true;
    public int AlertPercentage { get; set; } = 80;
}

public sealed class AiHistoryOptions
{
    public bool Enabled { get; set; } = true;
    public int RetentionDays { get; set; } = 90;
    public int MaxMessagesPerConversation { get; set; } = 50;
    public bool StoreToolResults { get; set; } = false;
}
