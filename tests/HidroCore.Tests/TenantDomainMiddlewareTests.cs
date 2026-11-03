using HidroCore.Domain;
using HidroCore.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace HidroCore.Tests;

public sealed class TenantDomainMiddlewareTests
{
    [Fact]
    public async Task Ignores_forwarded_host_and_rejects_unknown_host()
    {
        var tenant = new Tenant("Onça", "onca");
        var options = new DbContextOptionsBuilder<HidroCoreDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using (var seed = new HidroCoreDbContext(options, new TenantContext()))
        {
            seed.Tenants.Add(tenant);
            seed.TenantDominios.Add(new TenantDominio(tenant.Id, "trusted.example"));
            await seed.SaveChangesAsync();
        }
        await using var db = new HidroCoreDbContext(options, new TenantContext());
        var resolver = new TenantDomainResolver(db);
        var tenantContext = new TenantContext();
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString("attacker.example");
        context.Request.Headers["X-Forwarded-Host"] = "trusted.example";
        var nextCalled = false;
        var middleware = new TenantDomainMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(context, resolver, tenantContext);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        Assert.Null(tenantContext.TenantId);
    }
}
