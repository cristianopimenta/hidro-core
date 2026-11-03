using Microsoft.Extensions.DependencyInjection;

namespace HidroCore.Integrations;

public static class DependencyInjection
{
    public static IServiceCollection AddHidroCoreIntegrations(this IServiceCollection services)
    {
        services.AddSingleton<IIntegrationModule>(_ => new DocumentedProtocolRequiredModule("ANA"));
        services.AddSingleton<IIntegrationModule>(_ => new DocumentedProtocolRequiredModule("CCEE"));
        services.AddSingleton<IntegrationModuleRegistry>();
        return services;
    }
}
