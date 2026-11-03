namespace HidroCore.Domain;

public sealed class Grandeza : ITenantEntity
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid TenantId { get; private set; }

    private Grandeza() { }
    public Grandeza(Guid tenantId, string nome, string unidade)
    {
        TenantId = tenantId; Nome = nome.Trim(); Unidade = unidade.Trim();
        if (tenantId == Guid.Empty || string.IsNullOrWhiteSpace(nome) || string.IsNullOrWhiteSpace(unidade)) throw new ArgumentException("Dados da grandeza inválidos.");
    }
    public string Nome { get; private set; } = "";
    public string Unidade { get; private set; } = "";
}
