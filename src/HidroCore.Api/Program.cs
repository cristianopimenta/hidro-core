using HidroCore.Api;
using HidroCore.Integrations;
using HidroCore.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddHidroCoreInfrastructure(builder.Configuration);
builder.Services.AddHidroCoreIntegrations();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.Authority = builder.Configuration["Authentication:Authority"];
    options.Audience = builder.Configuration["Authentication:Audience"];
    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    options.MapInboundClaims = false;
});
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("TenantUser", policy => policy.RequireAuthenticatedUser().AddRequirements(new TenantMatchRequirement()));
    options.AddPolicy("TenantRead", policy => policy.RequireAuthenticatedUser().AddRequirements(new TenantMatchRequirement()));
    options.AddPolicy("TenantCatalogAdmin", policy => policy.RequireAuthenticatedUser().AddRequirements(new TenantRoleRequirement("Administrador")));
    options.AddPolicy("TenantReadingWrite", policy => policy.RequireAuthenticatedUser().AddRequirements(new TenantRoleRequirement("Administrador", "Operador")));
});
builder.Services.AddScoped<IAuthorizationHandler, TenantMatchHandler>();
builder.Services.AddScoped<IAuthorizationHandler, TenantRoleHandler>();

var app = builder.Build();
if (app.Environment.IsDevelopment()) app.MapOpenApi();
app.UseMiddleware<TenantDomainMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health");
app.MapGet("/", () => Results.Ok(new { service = "Hidro-Core API", status = "ready" }));
app.MapGet("/api/tenant", (TenantContext tenant) => Results.Ok(new { tenantId = tenant.TenantId }))
    .RequireAuthorization("TenantUser");
app.MapGet("/api/branding", async (TenantContext tenant, HidroCoreDbContext db, CancellationToken cancellationToken) =>
{
    var branding = await db.Tenants.AsNoTracking().Where(x => x.Id == tenant.TenantId)
        .Select(x => new { x.Nome, x.CorPrimaria, x.LogoUrl }).SingleOrDefaultAsync(cancellationToken);
    return branding is null ? Results.NotFound() : Results.Ok(branding);
}).RequireAuthorization("TenantUser");
app.MapHidroCoreEndpoints();
app.Run();
