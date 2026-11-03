namespace HidroCore.Domain;

public sealed class Usina : ITenantEntity
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid TenantId { get; private set; }

    private Usina() { }
    public Usina(Guid tenantId, string nome, string codigo)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId inválido.", nameof(tenantId));
        TenantId = tenantId; Nome = Validate(nome, nameof(nome), 160); Codigo = Validate(codigo, nameof(codigo), 80);
    }
    public string Nome { get; private set; } = "";
    public string Codigo { get; private set; } = "";

    public void Atualizar(string nome, string codigo)
    {
        var validatedName = Validate(nome, nameof(nome), 160);
        var validatedCode = Validate(codigo, nameof(codigo), 80);
        Nome = validatedName;
        Codigo = validatedCode;
    }

    private static string Validate(string value, string parameter, int maxLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameter);
        var normalized = value.Trim();
        if (normalized.Length > maxLength) throw new ArgumentException($"Máximo de {maxLength} caracteres.", parameter);
        return normalized;
    }
}
