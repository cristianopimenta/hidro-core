namespace HidroCore.Domain;

public sealed class IntegracaoUsina : ITenantEntity
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid TenantId { get; private set; }

    private IntegracaoUsina() { }
    public IntegracaoUsina(Guid tenantId, Guid usinaId, string modulo, string configuracaoJson, string? cronColeta = null, string? cronTransmissao = null)
    {
        if (tenantId == Guid.Empty || usinaId == Guid.Empty || string.IsNullOrWhiteSpace(modulo)) throw new ArgumentException("Dados da integração inválidos.");
        TenantId = tenantId; UsinaId = usinaId; Modulo = modulo.Trim().ToUpperInvariant(); ConfiguracaoJson = configuracaoJson; CronColeta = cronColeta; CronTransmissao = cronTransmissao; Habilitada = true;
    }
    public Guid UsinaId { get; private set; }
    public string Modulo { get; private set; } = "";
    public string ConfiguracaoJson { get; private set; } = "{}";
    public string? CronColeta { get; private set; }
    public string? CronTransmissao { get; private set; }
    public bool Habilitada { get; private set; }
}
