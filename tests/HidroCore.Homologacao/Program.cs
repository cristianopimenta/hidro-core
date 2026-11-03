using HidroCore.Domain;
using HidroCore.Infrastructure;
using Microsoft.EntityFrameworkCore;

const string oncaIdText = "11111111-1111-4111-8111-111111111111";
const string santaIdText = "22222222-2222-4222-8222-222222222222";
var connectionString = Environment.GetEnvironmentVariable("HIDROCORE_HOMOLOGACAO_CONNECTION")
    ?? throw new InvalidOperationException("Defina HIDROCORE_HOMOLOGACAO_CONNECTION para o PostgreSQL descartável.");
var oncaId = Guid.Parse(oncaIdText);
var santaId = Guid.Parse(santaIdText);
var options = new DbContextOptionsBuilder<HidroCoreDbContext>().UseNpgsql(connectionString).Options;

await VerifyTenantAsync(options, oncaId, "ONCA-HML-01", "SANTA-HML-01", "ANA", "CCEE");
await VerifyTenantAsync(options, santaId, "SANTA-HML-01", "ONCA-HML-01", "ANA", "CCEE");
await VerifyCrossTenantWriteRejectedAsync(options, oncaId, santaId);
Console.WriteLine("PASS: filtros de leitura, persistência PostgreSQL, múltiplos módulos e bloqueio de gravação cross-tenant.");

static async Task VerifyTenantAsync(DbContextOptions<HidroCoreDbContext> options, Guid tenantId, string ownCode, string otherCode, string moduleA, string moduleB)
{
    var tenantContext = new TenantContext();
    tenantContext.SetTenant(tenantId);
    await using var db = new HidroCoreDbContext(options, tenantContext);
    var codes = await db.Usinas.AsNoTracking().Select(x => x.Codigo).ToListAsync();
    Ensure(codes.Contains(ownCode), $"Tenant {tenantId} não leu sua usina.");
    Ensure(!codes.Contains(otherCode), $"Tenant {tenantId} leu a usina de outro tenant.");
    Ensure(await db.Leituras.CountAsync() == 1, $"Tenant {tenantId} não isolou as leituras de teste.");
    Ensure(await db.Database.SqlQueryRaw<string>("SELECT \"Version\" AS \"Value\" FROM schema_migrations WHERE \"Version\" = '0001_initial_multitenant'").AnyAsync(), "Migration não registrada no banco local.");
    var modules = await db.IntegracoesUsina.AsNoTracking().Select(x => x.Modulo).ToListAsync();
    Ensure(modules.Count(x => x == moduleA) == 1 && modules.Count(x => x == moduleB) == 1, $"Tenant {tenantId} não leu suas integrações ANA/CCEE.");

    const string markerCode = "HML-PERSISTENCIA-ONCA-01";
    if (tenantId == Guid.Parse("11111111-1111-4111-8111-111111111111") && !await db.Usinas.AnyAsync(x => x.Codigo == markerCode))
    {
        db.Usinas.Add(new Usina(tenantId, "Registro persistente de homologação", markerCode));
        await db.SaveChangesAsync();
    }
    if (tenantId == Guid.Parse("11111111-1111-4111-8111-111111111111"))
        Ensure(await db.Usinas.AnyAsync(x => x.Codigo == markerCode), "Registro de persistência não foi gravado.");
}

static async Task VerifyCrossTenantWriteRejectedAsync(DbContextOptions<HidroCoreDbContext> options, Guid tenantId, Guid otherTenantId)
{
    var tenantContext = new TenantContext();
    tenantContext.SetTenant(tenantId);
    await using var db = new HidroCoreDbContext(options, tenantContext);
    db.Usinas.Add(new Usina(otherTenantId, "Tentativa cross-tenant", "HML-CROSS-TENANT-REJEITADA"));
    try
    {
        await db.SaveChangesAsync();
        throw new InvalidOperationException("A gravação cross-tenant foi aceita inesperadamente.");
    }
    catch (InvalidOperationException exception) when (exception.Message.Contains("tenant diferente", StringComparison.OrdinalIgnoreCase))
    {
        // Expected guard from HidroCoreDbContext.
    }
}

static void Ensure(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
