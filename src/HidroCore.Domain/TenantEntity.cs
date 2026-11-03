namespace HidroCore.Domain;

public interface ITenantEntity
{
    Guid Id { get; }
    Guid TenantId { get; }
}
