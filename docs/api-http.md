# API HTTP — Hidro-Core

Base: `/api`. Respostas usam JSON camelCase. Todas as rotas exigem um hostname ativo cadastrado e JWT válido do issuer configurado. O `TenantId` é derivado do domínio no servidor e comparado com o claim JWT `tenant_id`; o `sub` também deve ter associação ativa em `tenant_usuarios` para o mesmo tenant. IDs e `TenantId` recebidos no corpo não são aceitos como autoridade.

## Permissões

| Operação | Papel exigido |
|---|---|
| GET de cadastros, grandezas e leituras | Qualquer usuário ativo do tenant |
| POST/PUT/DELETE de usinas, estações e equipamentos | `Administrador` |
| POST de leituras | `Administrador` ou `Operador` |

Papéis são lidos da associação `tenant_usuarios`, não de claims de papel fornecidos pelo cliente.

## Endpoints

| Método e caminho | Corpo/filtros | Resposta |
|---|---|---|
| `GET /api/usinas` | — | `200 UsinaResponse[]` |
| `GET /api/usinas/{id}` | UUID | `200 UsinaResponse`, `404` fora do tenant/não existe |
| `POST /api/usinas` | `{ "nome": "UHE Rio", "codigo": "RIO-01" }` | `201 UsinaResponse` |
| `PUT /api/usinas/{id}` | mesmo corpo do POST | `200 UsinaResponse` |
| `DELETE /api/usinas/{id}` | — | `204`; `409` se houver estações vinculadas |
| `GET /api/estacoes?usinaId={uuid}` | `usinaId` opcional | `200 EstacaoResponse[]` |
| `GET /api/estacoes/{id}` | UUID | `200 EstacaoResponse` |
| `POST /api/estacoes` | `{ "usinaId": "…", "nome": "Casa de força", "codigo": "CF-01" }` | `201 EstacaoResponse` |
| `PUT /api/estacoes/{id}` | mesmo corpo do POST | `200 EstacaoResponse` |
| `DELETE /api/estacoes/{id}` | — | `204`; `409` se houver equipamentos |
| `GET /api/equipamentos?estacaoId={uuid}` | `estacaoId` opcional | `200 EquipamentoResponse[]` |
| `GET /api/equipamentos/{id}` | UUID | `200 EquipamentoResponse` |
| `POST /api/equipamentos` | `{ "estacaoId": "…", "nome": "Sensor nível", "codigo": "NIV-01" }` | `201 EquipamentoResponse` |
| `PUT /api/equipamentos/{id}` | mesmo corpo do POST | `200 EquipamentoResponse` |
| `DELETE /api/equipamentos/{id}` | — | `204`; `409` se houver leituras |
| `GET /api/grandezas` | — | `200 GrandezaResponse[]` do tenant |
| `GET /api/leituras/{id}` | UUID | `200 LeituraResponse` |
| `GET /api/leituras` | ver filtros abaixo | `200 PagedResponse<LeituraResponse>` |
| `POST /api/leituras` | `{ "equipamentoId": "…", "grandezaId": "…", "instante": "2026-11-02T12:00:00Z", "valor": 100.125 }` | `201 LeituraResponse` |

Leituras aceitam `estacaoId`, `grandezaId`, `inicio`, `fim`, `page` e `pageSize`. Período é inclusivo. `page` vai de 1 a 1.000.000 e `pageSize` de 1 a 200; padrões 1 e 100. Ordem: instante decrescente, depois ID crescente.

Valores de leitura precisam caber em `NUMERIC(20,6)` e não podem ter mais de seis casas decimais. A chave `(TenantId, EquipamentoId, GrandezaId, Instante)` é única; duplicidade retorna `409`. Referências inexistentes ou de outro tenant retornam `404`. Dados inválidos retornam `400` em `ValidationProblemDetails`; ausência de autenticação retorna `401`, falta de permissão `403`.

## Exemplo de consulta

```http
GET /api/leituras?estacaoId=...&grandezaId=...&inicio=2026-11-01T00%3A00%3A00Z&fim=2026-11-02T23%3A59%3A59Z&page=1&pageSize=50
Authorization: Bearer <access-token>
Host: onca.hidro.test
```

`GET /openapi/v1.json` disponibiliza o documento OpenAPI no ambiente Development.
