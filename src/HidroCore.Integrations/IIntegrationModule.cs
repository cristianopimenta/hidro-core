namespace HidroCore.Integrations;

public interface IIntegrationModule
{
    string Code { get; }
    bool SupportsCollection { get; }
    bool SupportsTransmission { get; }
    Task CollectAsync(Guid tenantId, Guid integrationId, CancellationToken cancellationToken);
    Task TransmitAsync(Guid tenantId, Guid integrationId, CancellationToken cancellationToken);
}

public sealed class IntegrationModuleRegistry(IEnumerable<IIntegrationModule> modules)
{
    private readonly IReadOnlyDictionary<string, IIntegrationModule> _modules = modules.ToDictionary(x => x.Code, StringComparer.OrdinalIgnoreCase);
    public bool TryGet(string code, out IIntegrationModule? module) => _modules.TryGetValue(code, out module);
}
