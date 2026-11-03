namespace HidroCore.Domain;

public sealed class Leitura : ITenantEntity
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid TenantId { get; private set; }

    private Leitura() { }
    public Leitura(Guid tenantId, Guid equipamentoId, Guid grandezaId, DateTimeOffset instante, decimal valor)
    {
        if (tenantId == Guid.Empty || equipamentoId == Guid.Empty || grandezaId == Guid.Empty) throw new ArgumentException("Referências de leitura inválidas.");
        if (instante == default) throw new ArgumentException("Instante da leitura inválido.", nameof(instante));
        const decimal maximumValue = 99_999_999_999_999.999999m;
        if (decimal.Round(valor, 6, MidpointRounding.ToZero) != valor || valor > maximumValue || valor < -maximumValue)
            throw new ArgumentOutOfRangeException(nameof(valor), "Valor excede NUMERIC(20,6).");
        TenantId = tenantId; EquipamentoId = equipamentoId; GrandezaId = grandezaId; Instante = instante.ToUniversalTime(); Valor = valor;
    }
    public Guid EquipamentoId { get; private set; }
    public Guid GrandezaId { get; private set; }
    public DateTimeOffset Instante { get; private set; }
    public decimal Valor { get; private set; }
}
