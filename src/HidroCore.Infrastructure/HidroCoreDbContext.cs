using HidroCore.Domain;
using Microsoft.EntityFrameworkCore;

namespace HidroCore.Infrastructure;

public sealed class HidroCoreDbContext(DbContextOptions<HidroCoreDbContext> options, TenantContext tenantContext) : DbContext(options)
{
    private Guid? CurrentTenantId => tenantContext.TenantId;
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantDominio> TenantDominios => Set<TenantDominio>();
    public DbSet<TenantUsuario> TenantUsuarios => Set<TenantUsuario>();
    public DbSet<Usina> Usinas => Set<Usina>();
    public DbSet<Estacao> Estacoes => Set<Estacao>();
    public DbSet<Equipamento> Equipamentos => Set<Equipamento>();
    public DbSet<Grandeza> Grandezas => Set<Grandeza>();
    public DbSet<Leitura> Leituras => Set<Leitura>();
    public DbSet<IntegracaoUsina> IntegracoesUsina => Set<IntegracaoUsina>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tenant>(e => { e.ToTable("tenants"); e.HasKey(x => x.Id); e.Property(x => x.Nome).HasMaxLength(160).IsRequired(); e.Property(x => x.Chave).HasMaxLength(100).IsRequired(); e.Property(x => x.CorPrimaria).HasMaxLength(7).IsRequired(); e.Property(x => x.LogoUrl).HasMaxLength(2048); e.HasIndex(x => x.Chave).IsUnique(); });
        modelBuilder.Entity<TenantDominio>(e => { e.ToTable("tenant_dominios"); e.HasKey(x => x.Hostname); e.Property(x => x.Hostname).HasMaxLength(253); e.HasIndex(x => x.TenantId); e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict); });
        modelBuilder.Entity<TenantUsuario>(e => { e.ToTable("tenant_usuarios"); e.HasKey(x => new { x.TenantId, x.IdentitySubject }); e.HasIndex(x => new { x.TenantId, x.Email }).IsUnique(); e.Property(x => x.Email).HasMaxLength(320); e.Property(x => x.IdentitySubject).HasMaxLength(200); e.Property(x => x.Papel).HasMaxLength(60).IsRequired(); e.HasQueryFilter(x => CurrentTenantId != null && x.TenantId == CurrentTenantId); e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade); });
        ConfigureTenantEntity<Usina>(modelBuilder, "usinas");
        modelBuilder.Entity<Usina>().Property(x => x.Nome).HasMaxLength(160).IsRequired();
        modelBuilder.Entity<Usina>().HasIndex(x => new { x.TenantId, x.Codigo }).IsUnique();
        ConfigureTenantEntity<Estacao>(modelBuilder, "estacoes");
        modelBuilder.Entity<Estacao>().Property(x => x.Nome).HasMaxLength(160).IsRequired();
        modelBuilder.Entity<Estacao>().HasIndex(x => new { x.TenantId, x.UsinaId, x.Codigo }).IsUnique();
        ConfigureTenantEntity<Equipamento>(modelBuilder, "equipamentos");
        modelBuilder.Entity<Equipamento>().Property(x => x.Nome).HasMaxLength(160).IsRequired();
        modelBuilder.Entity<Equipamento>().HasIndex(x => new { x.TenantId, x.EstacaoId, x.Codigo }).IsUnique();
        ConfigureTenantEntity<Grandeza>(modelBuilder, "grandezas");
        modelBuilder.Entity<Grandeza>().Property(x => x.Nome).HasMaxLength(120).IsRequired();
        modelBuilder.Entity<Grandeza>().Property(x => x.Unidade).HasMaxLength(40).IsRequired();
        modelBuilder.Entity<Grandeza>().HasIndex(x => new { x.TenantId, x.Nome }).IsUnique();
        ConfigureTenantEntity<Leitura>(modelBuilder, "leituras");
        modelBuilder.Entity<Leitura>().Property(x => x.Instante).IsRequired();
        ConfigureTenantEntity<IntegracaoUsina>(modelBuilder, "integracoes_usina");
        modelBuilder.Entity<IntegracaoUsina>().Property(x => x.Modulo).HasMaxLength(80);
        modelBuilder.Entity<IntegracaoUsina>().Property(x => x.CronColeta).HasMaxLength(100);
        modelBuilder.Entity<IntegracaoUsina>().Property(x => x.CronTransmissao).HasMaxLength(100);
        modelBuilder.Entity<Leitura>().Property(x => x.Valor).HasPrecision(20, 6);
        modelBuilder.Entity<IntegracaoUsina>().Property(x => x.ConfiguracaoJson).HasColumnType("jsonb");
        modelBuilder.Entity<Leitura>().HasIndex(x => new { x.TenantId, x.EquipamentoId, x.GrandezaId, x.Instante }).IsUnique();
        modelBuilder.Entity<Estacao>().HasOne<Usina>().WithMany().HasForeignKey(x => new { x.TenantId, x.UsinaId }).HasPrincipalKey(x => new { x.TenantId, x.Id });
        modelBuilder.Entity<Equipamento>().HasOne<Estacao>().WithMany().HasForeignKey(x => new { x.TenantId, x.EstacaoId }).HasPrincipalKey(x => new { x.TenantId, x.Id });
        modelBuilder.Entity<Leitura>().HasOne<Equipamento>().WithMany().HasForeignKey(x => new { x.TenantId, x.EquipamentoId }).HasPrincipalKey(x => new { x.TenantId, x.Id });
        modelBuilder.Entity<Leitura>().HasOne<Grandeza>().WithMany().HasForeignKey(x => new { x.TenantId, x.GrandezaId }).HasPrincipalKey(x => new { x.TenantId, x.Id });
        modelBuilder.Entity<IntegracaoUsina>().HasOne<Usina>().WithMany().HasForeignKey(x => new { x.TenantId, x.UsinaId }).HasPrincipalKey(x => new { x.TenantId, x.Id });
    }

    private void ConfigureTenantEntity<TEntity>(ModelBuilder modelBuilder, string table) where TEntity : class, ITenantEntity
    {
        var entity = modelBuilder.Entity<TEntity>();
        entity.ToTable(table); entity.HasKey(x => x.Id); entity.HasAlternateKey(x => new { x.TenantId, x.Id });
        entity.Property(x => x.TenantId).IsRequired(); entity.HasQueryFilter(x => CurrentTenantId != null && x.TenantId == CurrentTenantId);
    }

    public override int SaveChanges()
    {
        EnforceTenantOnWrites();
        return base.SaveChanges();
    }
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        EnforceTenantOnWrites();
        return base.SaveChangesAsync(cancellationToken);
    }
    private void EnforceTenantOnWrites()
    {
        foreach (var entry in ChangeTracker.Entries<TenantUsuario>().Where(x => x.State is EntityState.Added or EntityState.Modified))
        {
            if (CurrentTenantId is null || entry.Entity.TenantId != CurrentTenantId)
                throw new InvalidOperationException("Usuário de outro tenant ou tenant ausente no contexto.");
        }
        foreach (var entry in ChangeTracker.Entries<ITenantEntity>().Where(x => x.State is EntityState.Added or EntityState.Modified))
        {
            if (CurrentTenantId is null) throw new InvalidOperationException("Tenant não definido para gravação.");
            if (entry.Entity.TenantId != CurrentTenantId) throw new InvalidOperationException("Gravação em tenant diferente do contexto recusada.");
        }
    }
}
