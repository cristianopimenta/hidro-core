using System.Security.Claims;
using HidroCore.Domain;
using HidroCore.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace HidroCore.Tests;

public sealed class TenantAuthorizationTests
{
    [Fact]
    public async Task Requires_matching_tenant_and_active_tenant_membership()
    {
        var tenantId = Guid.NewGuid();
        var tenantContext = new TenantContext(); tenantContext.SetTenant(tenantId);
        var options = new DbContextOptionsBuilder<HidroCoreDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using (var setup = new HidroCoreDbContext(options, tenantContext))
        {
            setup.TenantUsuarios.Add(new TenantUsuario(tenantId, "user-123", "user@example.com"));
            await setup.SaveChangesAsync();
        }
        await using var database = new HidroCoreDbContext(options, tenantContext);
        var handler = new TenantMatchHandler(tenantContext, database);
        Assert.True(await Authorize(handler, new Claim("tenant_id", tenantId.ToString()), new Claim("sub", "user-123")));
        Assert.False(await Authorize(handler, new Claim("tenant_id", Guid.NewGuid().ToString()), new Claim("sub", "user-123")));
        Assert.False(await Authorize(handler, new Claim("tenant_id", tenantId.ToString()), new Claim("sub", "unknown")));
        Assert.False(await Authorize(handler, new Claim("tenant_id", tenantId.ToString())));
    }

    private static async Task<bool> Authorize(TenantMatchHandler handler, params Claim[] claims)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        var context = new AuthorizationHandlerContext([new TenantMatchRequirement()], principal, null);
        await handler.HandleAsync(context);
        return context.HasSucceeded;
    }
}
