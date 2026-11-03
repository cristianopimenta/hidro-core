namespace HidroCore.Domain;

public sealed class Estacao : ITenantEntity
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid TenantId { get; private set; }

    private Estacao() { }
    public Estacao(Guid tenantId, Guid usinaId, string nome, string codigo)
    {
        if (tenantId == Guid.Empty || usinaId == Guid.Empty) throw new ArgumentException("TenantId/UsinaId inválido.");
        TenantId = tenantId; UsinaId = usinaId;
        Nome = Validate(nome, nameof(nome), 160); Codigo = Validate(codigo, nameof(codigo), 80);
    }
    public Guid UsinaId { get; private set; }
    public string Nome { get; private set; } = "";
    public string Codigo { get; private set; } = "";

    public void Atualizar(Guid usinaId, string nome, string codigo)
    {
        if (usinaId == Guid.Empty) throw new ArgumentException("UsinaId inválido.", nameof(usinaId));
        var validatedName = Validate(nome, nameof(nome), 160);
        var validatedCode = Validate(codigo, nameof(codigo), 80);
        UsinaId = usinaId; Nome = validatedName; Codigo = validatedCode;
    }

    private static string Validate(string value, string parameter, int maxLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameter);
        var normalized = value.Trim();
        if (normalized.Length > maxLength) throw new ArgumentException($"Máximo de {maxLength} caracteres.", parameter);
        return normalized;
    }
}
