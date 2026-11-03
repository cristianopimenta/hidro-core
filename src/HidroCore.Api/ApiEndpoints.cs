using HidroCore.Domain;
using HidroCore.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HidroCore.Api;

public static class ApiEndpoints
{
    public static IEndpointRouteBuilder MapHidroCoreEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup("/api");
        var read = api.MapGroup("").RequireAuthorization("TenantRead");
        var admin = api.MapGroup("").RequireAuthorization("TenantCatalogAdmin");
        var readingWriter = api.MapGroup("").RequireAuthorization("TenantReadingWrite");

        read.MapGet("/usinas", async (HidroCoreDbContext db, CancellationToken ct) =>
            Results.Ok(await db.Usinas.AsNoTracking().OrderBy(x => x.Nome)
                .Select(x => new UsinaResponse(x.Id, x.Nome, x.Codigo)).ToListAsync(ct)))
            .WithSummary("Lista usinas do tenant ativo.");
        read.MapGet("/usinas/{id:guid}", async Task<IResult> (Guid id, HidroCoreDbContext db, CancellationToken ct) =>
        {
            var entity = await db.Usinas.AsNoTracking().Where(x => x.Id == id)
                .Select(x => new UsinaResponse(x.Id, x.Nome, x.Codigo)).SingleOrDefaultAsync(ct);
            return entity is null ? Results.NotFound() : Results.Ok(entity);
        }).WithSummary("Consulta uma usina do tenant ativo.");
        admin.MapPost("/usinas", async Task<IResult> (UsinaRequest? request, TenantContext tenant, HidroCoreDbContext db, CancellationToken ct) =>
        {
            if (request is null) return Invalid("body", "Corpo da requisição obrigatório.");
            try
            {
                var entity = new Usina(RequireTenant(tenant), request.Nome!, request.Codigo!);
                db.Usinas.Add(entity);
                await db.SaveChangesAsync(ct);
                return Results.Created($"/api/usinas/{entity.Id}", ToResponse(entity));
            }
            catch (ArgumentException exception) { return Invalid(exception.ParamName ?? "request", exception.Message); }
            catch (DbUpdateException exception) when (IsConstraintConflict(exception)) { return Conflict("Usina com código duplicado ou referência inválida."); }
        }).WithSummary("Cria uma usina. Requer papel Administrador.");
        admin.MapPut("/usinas/{id:guid}", async Task<IResult> (Guid id, UsinaRequest? request, HidroCoreDbContext db, CancellationToken ct) =>
        {
            if (request is null) return Invalid("body", "Corpo da requisição obrigatório.");
            var entity = await db.Usinas.SingleOrDefaultAsync(x => x.Id == id, ct);
            if (entity is null) return Results.NotFound();
            try
            {
                entity.Atualizar(request.Nome!, request.Codigo!);
                await db.SaveChangesAsync(ct);
                return Results.Ok(ToResponse(entity));
            }
            catch (ArgumentException exception) { return Invalid(exception.ParamName ?? "request", exception.Message); }
            catch (DbUpdateException exception) when (IsConstraintConflict(exception)) { return Conflict("Código duplicado."); }
        }).WithSummary("Atualiza uma usina. Requer papel Administrador.");
        admin.MapDelete("/usinas/{id:guid}", async (Guid id, HidroCoreDbContext db, CancellationToken ct) =>
            await DeleteAsync(await db.Usinas.SingleOrDefaultAsync(x => x.Id == id, ct), db, ct)).WithSummary("Remove uma usina sem estações vinculadas. Requer papel Administrador.");

        read.MapGet("/estacoes", async (Guid? usinaId, HidroCoreDbContext db, CancellationToken ct) =>
        {
            var query = db.Estacoes.AsNoTracking();
            if (usinaId.HasValue) query = query.Where(x => x.UsinaId == usinaId.Value);
            return Results.Ok(await query.OrderBy(x => x.Nome)
                .Select(x => new EstacaoResponse(x.Id, x.UsinaId, x.Nome, x.Codigo)).ToListAsync(ct));
        }).WithSummary("Lista estações do tenant; aceita filtro por usinaId.");
        read.MapGet("/estacoes/{id:guid}", async Task<IResult> (Guid id, HidroCoreDbContext db, CancellationToken ct) =>
        {
            var entity = await db.Estacoes.AsNoTracking().Where(x => x.Id == id)
                .Select(x => new EstacaoResponse(x.Id, x.UsinaId, x.Nome, x.Codigo)).SingleOrDefaultAsync(ct);
            return entity is null ? Results.NotFound() : Results.Ok(entity);
        }).WithSummary("Consulta uma estação do tenant ativo.");
        admin.MapPost("/estacoes", async Task<IResult> (EstacaoRequest? request, TenantContext tenant, HidroCoreDbContext db, CancellationToken ct) =>
        {
            if (request is null) return Invalid("body", "Corpo da requisição obrigatório.");
            if (!await db.Usinas.AnyAsync(x => x.Id == request.UsinaId, ct)) return Results.NotFound();
            try
            {
                var entity = new Estacao(RequireTenant(tenant), request.UsinaId, request.Nome!, request.Codigo!);
                db.Estacoes.Add(entity);
                await db.SaveChangesAsync(ct);
                return Results.Created($"/api/estacoes/{entity.Id}", ToResponse(entity));
            }
            catch (ArgumentException exception) { return Invalid(exception.ParamName ?? "request", exception.Message); }
            catch (DbUpdateException exception) when (IsConstraintConflict(exception)) { return Conflict("Código duplicado ou referência inválida."); }
        }).WithSummary("Cria uma estação em uma usina do tenant. Requer Administrador.");
        admin.MapPut("/estacoes/{id:guid}", async Task<IResult> (Guid id, EstacaoRequest? request, HidroCoreDbContext db, CancellationToken ct) =>
        {
            if (request is null) return Invalid("body", "Corpo da requisição obrigatório.");
            var entity = await db.Estacoes.SingleOrDefaultAsync(x => x.Id == id, ct);
            if (entity is null) return Results.NotFound();
            if (!await db.Usinas.AnyAsync(x => x.Id == request.UsinaId, ct)) return Results.NotFound();
            try
            {
                entity.Atualizar(request.UsinaId, request.Nome!, request.Codigo!);
                await db.SaveChangesAsync(ct);
                return Results.Ok(ToResponse(entity));
            }
            catch (ArgumentException exception) { return Invalid(exception.ParamName ?? "request", exception.Message); }
            catch (DbUpdateException exception) when (IsConstraintConflict(exception)) { return Conflict("Código duplicado ou referência inválida."); }
        }).WithSummary("Atualiza uma estação. Requer Administrador.");
        admin.MapDelete("/estacoes/{id:guid}", async (Guid id, HidroCoreDbContext db, CancellationToken ct) =>
            await DeleteAsync(await db.Estacoes.SingleOrDefaultAsync(x => x.Id == id, ct), db, ct)).WithSummary("Remove uma estação sem equipamentos vinculados. Requer Administrador.");

        read.MapGet("/equipamentos", async (Guid? estacaoId, HidroCoreDbContext db, CancellationToken ct) =>
        {
            var query = db.Equipamentos.AsNoTracking();
            if (estacaoId.HasValue) query = query.Where(x => x.EstacaoId == estacaoId.Value);
            return Results.Ok(await query.OrderBy(x => x.Nome)
                .Select(x => new EquipamentoResponse(x.Id, x.EstacaoId, x.Nome, x.Codigo)).ToListAsync(ct));
        }).WithSummary("Lista equipamentos do tenant; aceita filtro por estacaoId.");
        read.MapGet("/equipamentos/{id:guid}", async Task<IResult> (Guid id, HidroCoreDbContext db, CancellationToken ct) =>
        {
            var entity = await db.Equipamentos.AsNoTracking().Where(x => x.Id == id)
                .Select(x => new EquipamentoResponse(x.Id, x.EstacaoId, x.Nome, x.Codigo)).SingleOrDefaultAsync(ct);
            return entity is null ? Results.NotFound() : Results.Ok(entity);
        }).WithSummary("Consulta um equipamento do tenant ativo.");
        admin.MapPost("/equipamentos", async Task<IResult> (EquipamentoRequest? request, TenantContext tenant, HidroCoreDbContext db, CancellationToken ct) =>
        {
            if (request is null) return Invalid("body", "Corpo da requisição obrigatório.");
            if (!await db.Estacoes.AnyAsync(x => x.Id == request.EstacaoId, ct)) return Results.NotFound();
            try
            {
                var entity = new Equipamento(RequireTenant(tenant), request.EstacaoId, request.Nome!, request.Codigo!);
                db.Equipamentos.Add(entity);
                await db.SaveChangesAsync(ct);
                return Results.Created($"/api/equipamentos/{entity.Id}", ToResponse(entity));
            }
            catch (ArgumentException exception) { return Invalid(exception.ParamName ?? "request", exception.Message); }
            catch (DbUpdateException exception) when (IsConstraintConflict(exception)) { return Conflict("Código duplicado ou referência inválida."); }
        }).WithSummary("Cria um equipamento em uma estação do tenant. Requer Administrador.");
        admin.MapPut("/equipamentos/{id:guid}", async Task<IResult> (Guid id, EquipamentoRequest? request, HidroCoreDbContext db, CancellationToken ct) =>
        {
            if (request is null) return Invalid("body", "Corpo da requisição obrigatório.");
            var entity = await db.Equipamentos.SingleOrDefaultAsync(x => x.Id == id, ct);
            if (entity is null) return Results.NotFound();
            if (!await db.Estacoes.AnyAsync(x => x.Id == request.EstacaoId, ct)) return Results.NotFound();
            try
            {
                entity.Atualizar(request.EstacaoId, request.Nome!, request.Codigo!);
                await db.SaveChangesAsync(ct);
                return Results.Ok(ToResponse(entity));
            }
            catch (ArgumentException exception) { return Invalid(exception.ParamName ?? "request", exception.Message); }
            catch (DbUpdateException exception) when (IsConstraintConflict(exception)) { return Conflict("Código duplicado ou referência inválida."); }
        }).WithSummary("Atualiza um equipamento. Requer Administrador.");
        admin.MapDelete("/equipamentos/{id:guid}", async (Guid id, HidroCoreDbContext db, CancellationToken ct) =>
            await DeleteAsync(await db.Equipamentos.SingleOrDefaultAsync(x => x.Id == id, ct), db, ct)).WithSummary("Remove um equipamento sem leituras vinculadas. Requer Administrador.");

        read.MapGet("/grandezas", async (HidroCoreDbContext db, CancellationToken ct) =>
            Results.Ok(await db.Grandezas.AsNoTracking().OrderBy(x => x.Nome)
                .Select(x => new GrandezaResponse(x.Id, x.Nome, x.Unidade)).ToListAsync(ct)))
            .WithSummary("Lista grandezas cadastradas para o tenant.");
        read.MapGet("/leituras/{id:guid}", async Task<IResult> (Guid id, HidroCoreDbContext db, CancellationToken ct) =>
        {
            var entity = await db.Leituras.AsNoTracking().Where(x => x.Id == id)
                .Select(x => new LeituraResponse(x.Id, x.EquipamentoId, x.GrandezaId, x.Instante, x.Valor)).SingleOrDefaultAsync(ct);
            return entity is null ? Results.NotFound() : Results.Ok(entity);
        }).WithSummary("Consulta uma leitura do tenant ativo.");
        read.MapGet("/leituras", async Task<IResult> (
            Guid? estacaoId, Guid? grandezaId, DateTimeOffset? inicio, DateTimeOffset? fim,
            int? page, int? pageSize, HidroCoreDbContext db, CancellationToken ct) =>
        {
            var currentPage = page ?? 1;
            var currentPageSize = pageSize ?? 100;
            if (currentPage < 1 || currentPage > 1_000_000 || currentPageSize < 1 || currentPageSize > 200)
                return Invalid("page/pageSize", "page deve estar entre 1 e 1000000 e pageSize entre 1 e 200.");
            if (inicio.HasValue && fim.HasValue && inicio.Value > fim.Value)
                return Invalid("inicio/fim", "inicio deve ser menor ou igual a fim.");
            if (estacaoId.HasValue && !await db.Estacoes.AnyAsync(x => x.Id == estacaoId.Value, ct)) return Results.NotFound();
            if (grandezaId.HasValue && !await db.Grandezas.AnyAsync(x => x.Id == grandezaId.Value, ct)) return Results.NotFound();

            var query = db.Leituras.AsNoTracking().AsQueryable();
            if (estacaoId.HasValue)
                query = query.Where(reading => db.Equipamentos.Any(equipment => equipment.Id == reading.EquipamentoId && equipment.EstacaoId == estacaoId.Value));
            if (grandezaId.HasValue) query = query.Where(reading => reading.GrandezaId == grandezaId.Value);
            if (inicio.HasValue) query = query.Where(reading => reading.Instante >= inicio.Value);
            if (fim.HasValue) query = query.Where(reading => reading.Instante <= fim.Value);

            var total = await query.LongCountAsync(ct);
            var items = await query.OrderByDescending(reading => reading.Instante).ThenBy(reading => reading.Id)
                .Skip((currentPage - 1) * currentPageSize).Take(currentPageSize)
                .Select(reading => new LeituraResponse(reading.Id, reading.EquipamentoId, reading.GrandezaId, reading.Instante, reading.Valor))
                .ToListAsync(ct);
            return Results.Ok(new PagedResponse<LeituraResponse>(items, currentPage, currentPageSize, total));
        }).WithSummary("Consulta leituras paginadas com filtros por estação, grandeza e período inclusivo.");
        readingWriter.MapPost("/leituras", async Task<IResult> (LeituraRequest? request, TenantContext tenant, HidroCoreDbContext db, CancellationToken ct) =>
        {
            if (request is null) return Invalid("body", "Corpo da requisição obrigatório.");
            if (!await db.Equipamentos.AnyAsync(x => x.Id == request.EquipamentoId, ct)) return Results.NotFound();
            if (!await db.Grandezas.AnyAsync(x => x.Id == request.GrandezaId, ct)) return Results.NotFound();
            var instante = request.Instante.ToUniversalTime();
            if (await db.Leituras.AnyAsync(x => x.EquipamentoId == request.EquipamentoId && x.GrandezaId == request.GrandezaId && x.Instante == instante, ct))
                return Conflict("Já existe leitura para o equipamento, grandeza e instante informados.");
            try
            {
                var entity = new Leitura(RequireTenant(tenant), request.EquipamentoId, request.GrandezaId, instante, request.Valor);
                db.Leituras.Add(entity);
                await db.SaveChangesAsync(ct);
                return Results.Created($"/api/leituras/{entity.Id}", ToResponse(entity));
            }
            catch (ArgumentException exception) { return Invalid(exception.ParamName ?? "request", exception.Message); }
            catch (DbUpdateException exception) when (IsConstraintConflict(exception)) { return Conflict("Duplicidade ou referência inválida."); }
        }).WithSummary("Grava leitura para equipamento e grandeza do tenant. Requer Administrador ou Operador.");

        return endpoints;
    }

    private static Guid RequireTenant(TenantContext context) => context.TenantId ?? throw new InvalidOperationException("Tenant ausente após resolução de domínio.");
    private static UsinaResponse ToResponse(Usina entity) => new(entity.Id, entity.Nome, entity.Codigo);
    private static EstacaoResponse ToResponse(Estacao entity) => new(entity.Id, entity.UsinaId, entity.Nome, entity.Codigo);
    private static EquipamentoResponse ToResponse(Equipamento entity) => new(entity.Id, entity.EstacaoId, entity.Nome, entity.Codigo);
    private static LeituraResponse ToResponse(Leitura entity) => new(entity.Id, entity.EquipamentoId, entity.GrandezaId, entity.Instante, entity.Valor);

    private static async Task<IResult> DeleteAsync<TEntity>(TEntity? entity, HidroCoreDbContext db, CancellationToken ct) where TEntity : class
    {
        if (entity is null) return Results.NotFound();
        db.Remove(entity);
        try { await db.SaveChangesAsync(ct); return Results.NoContent(); }
        catch (DbUpdateException exception) when (IsConstraintConflict(exception)) { return Conflict("Registro possui referências vinculadas ou conflita com dados existentes."); }
    }

    private static IResult Invalid(string key, string message) => Results.ValidationProblem(new Dictionary<string, string[]> { [key] = [message] });
    private static IResult Conflict(string message) => Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Conflito de dados", detail: message);
    private static bool IsConstraintConflict(DbUpdateException exception) => exception.GetBaseException() is PostgresException postgres &&
        postgres.SqlState is PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.ForeignKeyViolation;
}
