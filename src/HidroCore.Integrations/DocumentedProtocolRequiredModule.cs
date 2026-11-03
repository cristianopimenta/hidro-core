namespace HidroCore.Integrations;

// Explicit placeholders: no HTTP requests or payload formats are assumed until official specs exist.
public sealed class DocumentedProtocolRequiredModule(string code) : IIntegrationModule
{
    public string Code => code;
    public bool SupportsCollection => false;
    public bool SupportsTransmission => false;
    public Task CollectAsync(Guid tenantId, Guid integrationId, CancellationToken cancellationToken) =>
        throw new NotSupportedException($"Integration {Code} is disabled pending official protocol documentation.");
    public Task TransmitAsync(Guid tenantId, Guid integrationId, CancellationToken cancellationToken) =>
        throw new NotSupportedException($"Integration {Code} is disabled pending official protocol documentation.");
}
