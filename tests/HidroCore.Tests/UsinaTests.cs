using HidroCore.Domain;

namespace HidroCore.Tests;

public sealed class UsinaTests
{
    [Fact]
    public void Constructor_requires_tenant_and_normalizes_names()
    {
        var tenantId = Guid.NewGuid();
        var usina = new Usina(tenantId, "  Cafeeira da Onça ", " CO-01 ");
        Assert.NotEqual(Guid.Empty, usina.Id);
        Assert.Equal(tenantId, usina.TenantId);
        Assert.Equal("Cafeeira da Onça", usina.Nome);
        Assert.Equal("CO-01", usina.Codigo);
    }

    [Fact]
    public void Constructor_rejects_missing_tenant()
    {
        Assert.Throws<ArgumentException>(() => new Usina(Guid.Empty, "UHE", "CO-01"));
    }
}
