namespace HidroCore.Domain;

public sealed class TenantUsuario
{
    private TenantUsuario() { }
    public TenantUsuario(Guid tenantId, string identitySubject, string email, string papel = "Operador")
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId inválido.", nameof(tenantId));
        ArgumentException.ThrowIfNullOrWhiteSpace(identitySubject); ArgumentException.ThrowIfNullOrWhiteSpace(email);
        TenantId = tenantId; IdentitySubject = identitySubject.Trim(); Email = email.Trim().ToLowerInvariant(); Papel = papel.Trim(); Ativo = true;
    }
    public Guid TenantId { get; private set; }
    public string IdentitySubject { get; private set; } = "";
    public string Email { get; private set; } = "";
    public bool Ativo { get; private set; }
    public string Papel { get; private set; } = "Operador";
}
