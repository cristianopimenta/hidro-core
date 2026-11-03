namespace HidroCore.Domain;

public sealed class Equipamento : ITenantEntity
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid TenantId { get; private set; }

    private Equipamento() { }
    public Equipamento(Guid tenantId, Guid estacaoId, string nome, string codigo)
    {
        if (tenantId == Guid.Empty || estacaoId == Guid.Empty) throw new ArgumentException("TenantId/EstacaoId inválido.");
        TenantId = tenantId; EstacaoId = estacaoId;
        Nome = Validate(nome, nameof(nome), 160); Codigo = Validate(codigo, nameof(codigo), 100);
    }
    public Guid EstacaoId { get; private set; }
    public string Nome { get; private set; } = "";
    public string Codigo { get; private set; } = "";

    public void Atualizar(Guid estacaoId, string nome, string codigo)
    {
        if (estacaoId == Guid.Empty) throw new ArgumentException("EstacaoId inválido.", nameof(estacaoId));
        var validatedName = Validate(nome, nameof(nome), 160);
        var validatedCode = Validate(codigo, nameof(codigo), 100);
        EstacaoId = estacaoId; Nome = validatedName; Codigo = validatedCode;
    }

    private static string Validate(string value, string parameter, int maxLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameter);
        var normalized = value.Trim();
        if (normalized.Length > maxLength) throw new ArgumentException($"Máximo de {maxLength} caracteres.", parameter);
        return normalized;
    }
}
