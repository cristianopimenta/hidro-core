using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HidroCore.Infrastructure;

public sealed class TenantMatchRequirement : IAuthorizationRequirement;

public sealed class TenantMatchHandler(TenantContext tenantContext, HidroCoreDbContext database) : AuthorizationHandler<TenantMatchRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, TenantMatchRequirement requirement)
    {
        var tenantClaim = context.User.FindFirstValue("tenant_id");
        var subject = context.User.FindFirstValue("sub");
        if (tenantContext.TenantId is not { } tenantId || !Guid.TryParse(tenantClaim, out var userTenantId) || userTenantId != tenantId || string.IsNullOrWhiteSpace(subject)) return;
        var isActiveMember = await database.TenantUsuarios.AsNoTracking().AnyAsync(user => user.IdentitySubject == subject && user.Ativo);
        if (isActiveMember) context.Succeed(requirement);
    }
}

public sealed class TenantRoleRequirement(params string[] allowedRoles) : IAuthorizationRequirement
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = allowedRoles;
}

public sealed class TenantRoleHandler(TenantContext tenantContext, HidroCoreDbContext database) : AuthorizationHandler<TenantRoleRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, TenantRoleRequirement requirement)
    {
        var tenantClaim = context.User.FindFirstValue("tenant_id");
        var subject = context.User.FindFirstValue("sub");
        if (tenantContext.TenantId is not { } tenantId || !Guid.TryParse(tenantClaim, out var claimedTenantId) || claimedTenantId != tenantId || string.IsNullOrWhiteSpace(subject)) return;

        var membership = await database.TenantUsuarios.AsNoTracking()
            .Where(user => user.IdentitySubject == subject && user.Ativo)
            .Select(user => user.Papel)
            .SingleOrDefaultAsync();
        if (membership is not null && requirement.AllowedRoles.Contains(membership, StringComparer.Ordinal))
            context.Succeed(requirement);
    }
}
