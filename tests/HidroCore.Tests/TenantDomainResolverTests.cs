using HidroCore.Domain;
using HidroCore.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HidroCore.Tests;

public sealed class TenantDomainResolverTests
{
    [Fact]
    public async Task Resolves_only_active_registered_tenant_domains()
    {
        var tenant = new Tenant("Cafeeira da Onça", "onca");
        var options = new DbContextOptionsBuilder<HidroCoreDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using (var seed = new HidroCoreDbContext(options, new TenantContext()))
        {
            seed.Tenants.Add(tenant);
            seed.TenantDominios.Add(new TenantDominio(tenant.Id, "onca.hidro.example"));
            await seed.SaveChangesAsync();
        }
        await using var db = new HidroCoreDbContext(options, new TenantContext());
        var resolver = new TenantDomainResolver(db);
        Assert.Equal(tenant.Id, await resolver.ResolveAsync("ONCA.HIDRO.EXAMPLE", default));
        Assert.Null(await resolver.ResolveAsync("intruso.hidro.example", default));
        Assert.Null(await resolver.ResolveAsync("onca.hidro.example.evil.test", default));
    }
}
