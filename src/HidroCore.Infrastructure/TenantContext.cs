namespace HidroCore.Infrastructure;

public sealed class TenantContext
{
    public Guid? TenantId { get; private set; }
    public void SetTenant(Guid tenantId)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId inválido.", nameof(tenantId));
        if (TenantId is not null && TenantId != tenantId) throw new InvalidOperationException("Tenant do contexto já definido.");
        TenantId = tenantId;
    }
}
