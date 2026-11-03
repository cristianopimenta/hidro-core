namespace HidroCore.Domain;

public sealed class TenantDominio
{
    private TenantDominio() { }
    public TenantDominio(Guid tenantId, string hostname)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId inválido.", nameof(tenantId));
        ArgumentException.ThrowIfNullOrWhiteSpace(hostname);
        if (hostname != hostname.Trim() || hostname.EndsWith("..", StringComparison.Ordinal)) throw new ArgumentException("Hostname inválido.", nameof(hostname));
        var host = hostname.EndsWith('.') ? hostname[..^1] : hostname;
        host = host.ToLowerInvariant();
        if (host.Length > 253 || host.Contains(':') || host.Contains('/') || host.Contains('@')) throw new ArgumentException("Hostname inválido.", nameof(hostname));
        try { host = new System.Globalization.IdnMapping().GetAscii(host); }
        catch (ArgumentException) { throw new ArgumentException("Hostname inválido.", nameof(hostname)); }
        if (!System.Text.RegularExpressions.Regex.IsMatch(host, "^(?=.{1,253}$)(?:[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?)(?:\\.(?:[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?))*$")) throw new ArgumentException("Hostname inválido.", nameof(hostname));
        TenantId = tenantId; Hostname = host; Ativo = true;
    }
    public Guid TenantId { get; private set; }
    public string Hostname { get; private set; } = "";
    public bool Ativo { get; private set; }
}
