using HidroCore.Infrastructure;

namespace HidroCore.Tests;

public sealed class TenantHostTests
{
    [Theory]
    [InlineData("PLANTA.Exemplo.com.", "planta.exemplo.com")]
    [InlineData("usina.com.br", "usina.com.br")]
    public void Normalizes_valid_hostnames(string input, string expected)
    {
        Assert.True(TenantHost.TryNormalize(input, out var host));
        Assert.Equal(expected, host);
    }

    [Theory]
    [InlineData("evil.example/path")]
    [InlineData("user@usina.example")]
    [InlineData("usina.example:443")]
    [InlineData("bad..example")]
    [InlineData("onca.hidro.example...")]
    [InlineData(" onca.hidro.example")]
    [InlineData(" ")]
    public void Rejects_invalid_hostnames(string input) => Assert.False(TenantHost.TryNormalize(input, out _));
}
