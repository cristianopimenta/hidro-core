using HidroCore.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.RegularExpressions;

namespace HidroCore.Infrastructure;

public sealed class TenantDomainResolver(HidroCoreDbContext database)
{
    public async Task<Guid?> ResolveAsync(string? rawHost, CancellationToken cancellationToken)
    {
        if (!TenantHost.TryNormalize(rawHost, out var hostname)) return null;
        return await database.TenantDominios.IgnoreQueryFilters().AsNoTracking()
            .Where(domain => domain.Ativo && domain.Hostname == hostname)
            .Join(database.Tenants.IgnoreQueryFilters().Where(tenant => tenant.Ativo), domain => domain.TenantId, tenant => tenant.Id, (_, tenant) => (Guid?)tenant.Id)
            .SingleOrDefaultAsync(cancellationToken);
    }
}

public static partial class TenantHost
{
    private static readonly Regex HostPattern = new("^(?=.{1,253}$)(?:[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?)(?:\\.(?:[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?))*$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static bool TryNormalize(string? rawHost, out string hostname)
    {
        hostname = string.Empty;
        if (string.IsNullOrWhiteSpace(rawHost) || rawHost != rawHost.Trim() || rawHost.Length > 300 || rawHost.Contains('/') || rawHost.Contains('@') || rawHost.Contains('\\') || rawHost.EndsWith("..", StringComparison.Ordinal)) return false;
        var candidate = rawHost.EndsWith('.') ? rawHost[..^1] : rawHost;
        if (candidate.Length == 0 || candidate.Contains(':')) return false;
        try { candidate = new IdnMapping().GetAscii(candidate).ToLowerInvariant(); }
        catch (ArgumentException) { return false; }
        if (!HostPattern.IsMatch(candidate)) return false;
        hostname = candidate;
        return true;
    }
}

public sealed class TenantDomainMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, TenantDomainResolver resolver, TenantContext tenantContext)
    {
        if (context.Request.Path == "/health" || context.Request.Path.StartsWithSegments("/openapi"))
        {
            await next(context);
            return;
        }

        // Deliberately use only the validated Host header. Forwarded headers are ignored unless
        // a deployment explicitly configures ASP.NET Core KnownProxies/KnownNetworks.
        var tenantId = await resolver.ResolveAsync(context.Request.Host.Host, context.RequestAborted);
        if (tenantId is null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsJsonAsync(new { error = "Domínio não cadastrado." }, context.RequestAborted);
            return;
        }
        tenantContext.SetTenant(tenantId.Value);
        context.Items["TenantId"] = tenantId.Value;
        await next(context);
    }
}
