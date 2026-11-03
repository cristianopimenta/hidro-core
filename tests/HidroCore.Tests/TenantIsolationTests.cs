using HidroCore.Domain;
using HidroCore.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HidroCore.Tests;

public sealed class TenantIsolationTests
{
    [Fact]
    public async Task Queries_and_writes_are_scoped_to_current_tenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<HidroCoreDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using (var seed = new HidroCoreDbContext(options, new TenantContext()))
        {
            seed.Tenants.AddRange(new Tenant("Onça", "onca"), new Tenant("Santa Maria", "santa-maria"));
            await seed.SaveChangesAsync();
        }
        // Use the real IDs from the seeded rows.
        await using var lookup = new HidroCoreDbContext(options, new TenantContext());
        var tenants = await lookup.Tenants.IgnoreQueryFilters().OrderBy(x => x.Chave).ToListAsync();
        tenantA = tenants.Single(x => x.Chave == "onca").Id;
        tenantB = tenants.Single(x => x.Chave == "santa-maria").Id;
        await using (var contextA = ForTenant(options, tenantA))
        {
            contextA.Usinas.Add(new Usina(tenantA, "UHE A", "A"));
            await contextA.SaveChangesAsync();
            Assert.Single(await contextA.Usinas.ToListAsync());
        }
        await using (var contextB = ForTenant(options, tenantB))
        {
            contextB.Usinas.Add(new Usina(tenantB, "UHE B", "B"));
            await contextB.SaveChangesAsync();
            Assert.Equal("UHE B", Assert.Single(await contextB.Usinas.ToListAsync()).Nome);
        }
        await using var verifyA = ForTenant(options, tenantA);
        Assert.Equal("UHE A", Assert.Single(await verifyA.Usinas.ToListAsync()).Nome);
        verifyA.Usinas.Add(new Usina(tenantB, "Cross tenant", "X"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => verifyA.SaveChangesAsync());
    }

    private static HidroCoreDbContext ForTenant(DbContextOptions<HidroCoreDbContext> options, Guid tenantId)
    {
        var context = new TenantContext(); context.SetTenant(tenantId);
        return new HidroCoreDbContext(options, context);
    }
}
