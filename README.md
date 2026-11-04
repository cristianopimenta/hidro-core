# Hidro-Core

Plataforma .NET 10 compartilhada por múltiplas usinas/tenants, com PostgreSQL, API, painel Blazor e Worker.

## Estrutura

- `Domain`: Tenant, TenantDominio, TenantUsuario, Usina, Estacao, Equipamento, Grandeza, Leitura e IntegracaoUsina.
- `Application`: casos de uso e políticas de aplicação.
- `Infrastructure`: EF Core, filtros TenantId, resolução estrita de hostname, autorização tenant-aware e PostgreSQL.
- `Integrations`: módulos registrados ANA/CCEE como desabilitados até protocolos aprovados.
- `Api`, `Web`, `Worker`, `Tests`.

Todas as entidades operacionais e usuários têm isolamento por TenantId no EF Core; gravações sem tenant ou com tenant divergente falham. A API valida JWT de um provedor externo e exige `tenant_id` e `sub`, além de associação ativa em `tenant_usuarios`. A resolução usa apenas o Host da requisição, normalizado para ASCII/IDN, e requer domínio ativo cadastrado para tenant ativo. `X-Forwarded-Host` é ignorado. Se um proxy for usado, configure apenas `KnownProxies`/`KnownNetworks` explicitamente antes de confiar em cabeçalhos encaminhados.

## Banco e migrations

`Database:Mode=Shared` usa `ConnectionStrings:HidroCore`; `Dedicated` usa `ConnectionStrings:HidroCoreDedicated`. O modo Dedicated representa uma implantação isolada apontada a seu próprio PostgreSQL. Nenhum processo aplica migrations ao iniciar.

A migration SQL inicial, versionada, está em `src/HidroCore.Infrastructure/Migrations/0001_initial_multitenant.sql`. Revise e aplique manualmente no banco alvo, mantendo o histórico em `schema_migrations`. Nenhuma migration foi aplicada nesta tarefa.

Para provisionar um tenant/domínio, gere um registro `tenants` e um `tenant_dominios` com hostname confirmado, depois associe usuários autorizados em `tenant_usuarios` com o `sub` do provedor de identidade. Não existe endpoint público para provisionamento administrativo. Tokens devem ser emitidos pelo provedor configurado em `Authentication:Authority`, conter audiência `Authentication:Audience`, `sub` e claim `tenant_id`. Configure via variáveis `Authentication__Authority` e `Authentication__Audience`.

## Execução local

```sh
cp .env.example .env
# Defina senha local segura e conexão/authority conforme seu ambiente
# Após aplicar a migration em um PostgreSQL de desenvolvimento:
docker compose -f compose.yml up --build
```

Endpoints: API `http://localhost:8080`, painel `http://localhost:8081`, health `/health`. Um domínio tenant deve estar provisionado para acessar rotas; domínios desconhecidos retornam 404.

## Implantação independente por usina

O Compose de produção por instalação está em [`compose.producao.yml`](compose.producao.yml), com proxy Nginx e parâmetros por usina descritos em [`docs/implantacao-independente.md`](docs/implantacao-independente.md). Cada usina deve usar `COMPOSE_PROJECT_NAME`, domínio, credenciais e volumes próprios. O Compose de homologação permanece separado.

## Integrações e Worker

`IntegracaoUsina` suporta vários módulos por usina e agenda configurável. O Worker percorre tenants isoladamente e só registra rotinas elegíveis. Coleta e transmissão externas permanecem desativadas: não há chamadas reais, inclusive para ANA/CCEE. CCEE depende de documentação oficial; nenhum endpoint, payload ou protocolo foi presumido.

## Pendências

- Confirmar domínios, identidade visual e usuários iniciais de cada tenant.
- Configurar provedor JWT e processo administrativo seguro para provisionar tenants, domínios e usuários.
- Revisar/aplicar migration no ambiente alvo e definir backups, observabilidade e limites de pool.
- Definir regras e cron schedules, sem habilitar operações externas até aprovações e especificações.

## API HTTP

Os endpoints REST, formatos JSON, filtros, paginação, papéis e respostas de erro estão documentados em [`docs/api-http.md`](docs/api-http.md). A especificação OpenAPI fica disponível em `/openapi/v1.json` no ambiente Development.
