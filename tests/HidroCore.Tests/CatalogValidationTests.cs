using HidroCore.Domain;

namespace HidroCore.Tests;

public sealed class CatalogValidationTests
{
    [Fact]
    public void Station_and_equipment_updates_keep_valid_parent_ids_and_trim_values()
    {
        var tenantId = Guid.NewGuid();
        var usinaId = Guid.NewGuid();
        var stationId = Guid.NewGuid();
        var station = new Estacao(tenantId, usinaId, " estação ", " E-01 ");
        station.Atualizar(usinaId, " estação atualizada ", " E-02 ");
        Assert.Equal("estação atualizada", station.Nome);
        Assert.Equal("E-02", station.Codigo);
        Assert.Throws<ArgumentException>(() => station.Atualizar(Guid.Empty, "Nome", "Código"));

        var equipment = new Equipamento(tenantId, stationId, " sensor ", " S-01 ");
        equipment.Atualizar(stationId, " sensor atualizado ", " S-02 ");
        Assert.Equal("sensor atualizado", equipment.Nome);
        Assert.Equal("S-02", equipment.Codigo);
    }

    [Fact]
    public void Reading_rejects_values_outside_database_precision()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Leitura(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, 0.0000001m));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Leitura(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, 100_000_000_000_000m));
    }
}
