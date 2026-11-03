using HidroCore.Domain;

namespace HidroCore.Tests;

public sealed class TenantBrandingTests
{
    [Fact]
    public void Branding_is_configurable_and_requires_https_logo()
    {
        var tenant = new Tenant("Onça", "onca");
        tenant.AtualizarIdentidadeVisual("#126ABC", "https://cdn.example/logo.png");
        Assert.Equal("#126ABC", tenant.CorPrimaria);
        Assert.Equal("https://cdn.example/logo.png", tenant.LogoUrl);
        Assert.Throws<ArgumentException>(() => tenant.AtualizarIdentidadeVisual("red", null));
        Assert.Throws<ArgumentException>(() => tenant.AtualizarIdentidadeVisual("#126ABC", "http://cdn.example/logo.png"));
    }
}
