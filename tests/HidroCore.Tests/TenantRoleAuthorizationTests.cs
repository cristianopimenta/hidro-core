using System.Security.Claims;
using HidroCore.Domain;
using HidroCore.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace HidroCore.Tests;

public sealed class TenantRoleAuthorizationTests
{
    [Fact]
    public async Task Catalog_admin_policy_denies_operator_but_reading_policy_allows_operator()
    {
        var tenantId = Guid.NewGuid();
        var tenantContext = new TenantContext(); tenantContext.SetTenant(tenantId);
        var options = new DbContextOptionsBuilder<HidroCoreDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using (var seed = new HidroCoreDbContext(options, tenantContext))
        {
            seed.TenantUsuarios.Add(new TenantUsuario(tenantId, "operator-1", "operator@example.test", "Operador"));
            await seed.SaveChangesAsync();
        }
        await using var db = new HidroCoreDbContext(options, tenantContext);
        var handler = new TenantRoleHandler(tenantContext, db);
        var principal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("sub", "operator-1"), new Claim("tenant_id", tenantId.ToString())
        ], "test"));

        Assert.False(await Authorize(handler, principal, "Administrador"));
        Assert.True(await Authorize(handler, principal, "Administrador", "Operador"));
        var foreign = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("sub", "operator-1"), new Claim("tenant_id", Guid.NewGuid().ToString())
        ], "test"));
        Assert.False(await Authorize(handler, foreign, "Administrador", "Operador"));
    }

    private static async Task<bool> Authorize(TenantRoleHandler handler, ClaimsPrincipal principal, params string[] roles)
    {
        var context = new AuthorizationHandlerContext([new TenantRoleRequirement(roles)], principal, null);
        await handler.HandleAsync(context);
        return context.HasSucceeded;
    }
}
