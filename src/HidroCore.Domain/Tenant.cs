namespace HidroCore.Domain;

public sealed class Tenant
{
    private Tenant() { }
    public Tenant(string nome, string chave)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nome);
        ArgumentException.ThrowIfNullOrWhiteSpace(chave);
        Id = Guid.NewGuid(); Nome = nome.Trim(); Chave = chave.Trim().ToLowerInvariant(); Ativo = true;

    }
    public Guid Id { get; private set; }
    public string Nome { get; private set; } = "";
    public string Chave { get; private set; } = "";
    public bool Ativo { get; private set; }
    public string CorPrimaria { get; private set; } = "#14532d";
    public string? LogoUrl { get; private set; }

    public void AtualizarIdentidadeVisual(string corPrimaria, string? logoUrl)
    {
        if (corPrimaria is null || !System.Text.RegularExpressions.Regex.IsMatch(corPrimaria, "^#[0-9A-Fa-f]{6}$"))
            throw new ArgumentException("Cor deve usar o formato #RRGGBB.", nameof(corPrimaria));
        if (!string.IsNullOrWhiteSpace(logoUrl) && (!Uri.TryCreate(logoUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps))
            throw new ArgumentException("Logo deve usar uma URL HTTPS válida.", nameof(logoUrl));
        CorPrimaria = corPrimaria.ToUpperInvariant();
        LogoUrl = string.IsNullOrWhiteSpace(logoUrl) ? null : logoUrl;
    }
}
