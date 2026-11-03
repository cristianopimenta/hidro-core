using HidroCore.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HidroCore.Worker;

public sealed class Worker(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var delaySeconds = Math.Clamp(configuration.GetValue("Worker:PollSeconds", 60), 5, 3600);
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await PlanScheduledWorkAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { logger.LogError(exception, "Falha ao planejar rotinas multi-tenant."); }
            await Task.Delay(TimeSpan.FromSeconds(delaySeconds), stoppingToken);
        }
    }

    private async Task PlanScheduledWorkAsync(CancellationToken cancellationToken)
    {
        await using var discoveryScope = scopeFactory.CreateAsyncScope();
        var discoveryDb = discoveryScope.ServiceProvider.GetRequiredService<HidroCoreDbContext>();
        var tenants = await discoveryDb.Tenants.IgnoreQueryFilters().AsNoTracking()
            .Where(tenant => tenant.Ativo).Select(tenant => tenant.Id).ToListAsync(cancellationToken);

        foreach (var tenantId in tenants)
        {
            await using var tenantScope = scopeFactory.CreateAsyncScope();
            var tenantContext = tenantScope.ServiceProvider.GetRequiredService<TenantContext>();
            tenantContext.SetTenant(tenantId);
            var db = tenantScope.ServiceProvider.GetRequiredService<HidroCoreDbContext>();
            var enabled = await db.IntegracoesUsina.AsNoTracking().Where(x => x.Habilitada).ToListAsync(cancellationToken);
            foreach (var integration in enabled)
                logger.LogInformation("Rotina elegível planejada: Tenant={TenantId}, Integração={IntegrationId}, Módulo={Module}. Execução externa desabilitada.", tenantId, integration.Id, integration.Modulo);
        }
    }
}
