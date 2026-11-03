namespace HidroCore.Api;

public sealed record UsinaRequest(string? Nome, string? Codigo);
public sealed record UsinaResponse(Guid Id, string Nome, string Codigo);
public sealed record EstacaoRequest(Guid UsinaId, string? Nome, string? Codigo);
public sealed record EstacaoResponse(Guid Id, Guid UsinaId, string Nome, string Codigo);
public sealed record EquipamentoRequest(Guid EstacaoId, string? Nome, string? Codigo);
public sealed record EquipamentoResponse(Guid Id, Guid EstacaoId, string Nome, string Codigo);
public sealed record GrandezaResponse(Guid Id, string Nome, string Unidade);
public sealed record LeituraRequest(Guid EquipamentoId, Guid GrandezaId, DateTimeOffset Instante, decimal Valor);
public sealed record LeituraResponse(Guid Id, Guid EquipamentoId, Guid GrandezaId, DateTimeOffset Instante, decimal Valor);
public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, long TotalCount);
