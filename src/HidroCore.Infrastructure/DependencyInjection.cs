using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HidroCore.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddHidroCoreInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var mode = configuration["Database:Mode"] ?? "Shared";
        if (!mode.Equals("Shared", StringComparison.OrdinalIgnoreCase) && !mode.Equals("Dedicated", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Database:Mode deve ser Shared ou Dedicated.");
        services.AddScoped<TenantContext>();
        var connectionName = mode.Equals("Dedicated", StringComparison.OrdinalIgnoreCase) ? "HidroCoreDedicated" : "HidroCore";
        var connectionString = configuration.GetConnectionString(connectionName);
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddDbContext<HidroCoreDbContext>(options => options.UseNpgsql(connectionString));
            services.AddScoped<TenantDomainResolver>();
        }
        return services;
    }
}
