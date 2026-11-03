# Homologação funcional local — Hidro-Core

**Status: executada em ambiente local descartável.** Nenhum acesso a VPS/produção, UsinasData ou Posto Jusante; nenhum deploy, push ou envio ANA/CCEE.

## Ambiente isolado

- Compose exclusivo: `compose.homologacao.yml`, projeto `hidrocore-homologacao-20261102`.
- Keycloak `26.2.5`, PostgreSQL `17-alpine`; banco `hidrocore_homologacao` no volume próprio do projeto.
- Portas presas ao loopback: Keycloak `127.0.0.1:18080`, API `:18090`, Web `:18091`. PostgreSQL não publica porta no host.
- Worker/PostgreSQL ficam somente na rede Docker `internal: true`; o Worker não tem interface na rede local-publicada nem saída externa.
- `.env.homologacao` contém apenas credenciais sintéticas locais e está ignorado pelo Git/Docker.
- Realm: `hidro-homologacao`. Issuer/JWKS: `http://keycloak:8080/realms/hidro-homologacao`. Audience: `hidrocore-api`.
- Usuários fictícios: `onca.teste` e `santa.teste`; seus `sub` são os IDs de usuário fixos do realm e `tenant_id` corresponde aos tenants seed.
- Hostnames gravados no banco descartável: `onca.hidro.test` e `santamaria.hidro.test`; os testes enviam `Host` diretamente. Nenhuma entrada em `/etc/hosts` foi necessária ou criada.

## Resultados

| Verificação | Resultado |
|---|---|
| Compose dedicado e portas livres | PASS |
| Build local da solução e publish das imagens API/Web/Worker | PASS, 0 warnings / 0 errors no build .NET |
| Migration `0001_initial_multitenant.sql` e seed | PASS; aplicados somente no PostgreSQL desse Compose |
| OIDC discovery, issuer, JWKS e claim `aud` | PASS |
| JWT válido assinado, `sub`, `tenant_id`, audience e validade | PASS; API respondeu 200 |
| Sem token e assinatura/audience adulteradas/incorretas | PASS; API respondeu 401 |
| Usuário de um tenant acessando o outro | PASS; API respondeu 403 |
| Hostnames ativos, domínio desconhecido e `X-Forwarded-Host` forjado | PASS; desconhecidos/forjados responderam 404 |
| Web por domínio conhecido/desconhecido | PASS; conhecido respondeu 200, desconhecido 404 |
| Filtros PostgreSQL para usinas/leituras, gravação própria e gravação cruzada | PASS; leitura isolada e escrita cross-tenant recusada |
| Reinício de PostgreSQL, Keycloak, API, Web e Worker | PASS; tenants, usuários e marcador de escrita permaneceram disponíveis depois do reinício |
| Worker/ANA/CCEE | PASS; logs mostram somente planejamento com “Execução externa desabilitada”; rede sem saída externa |
| Testes automatizados xUnit | PASS: 16/16 |
| JWT expirado após clock skew padrão de 5 min | PASS; token rejeitado com 401 após `exp` + 305 s |

Na homologação inicial, a API ainda não tinha CRUD e o isolamento foi testado pelo harness EF Core. Na continuação registrada abaixo, foram adicionados endpoints REST e o isolamento CRUD foi validado por HTTP real. O issuer no claim `iss` correspondeu ao discovery/Authority.

## Falhas encontradas e correções locais

1. **Rede inicialmente toda `internal`:** bloqueou também as portas publicadas e a conexão local do harness. A rede foi separada: `isolated` para banco/Worker e `local-access` apenas para API, Web e Keycloak. PostgreSQL continuou sem porta publicada; o harness passou a rodar como container de uso único na rede interna.
2. **Keycloak retornou `invalid_grant` / “Account is not fully set up”:** usuários importados não tinham nome/sobrenome definidos. Corrigi o realm de teste e os perfis fictícios; a emissão de tokens passou.
3. **Aviso `libgssapi_krb5.so.2` no cliente PostgreSQL:** desabilitei GSS encryption apenas nas connection strings de homologação. Harness e Worker passaram sem esse aviso.
4. O Worker iniciou alguns segundos antes da aplicação manual da migration e registrou erro de tabela ausente no primeiro ciclo. Após migration e reinício dos serviços, opera normalmente; a rede e os logs finais confirmam ausência de chamadas externas.
5. A primeira chamada de expiração foi bloqueada pelo sandbox ao abrir loopback; repetida com acesso local aprovado, retornou 401 como esperado.

## Artefatos locais

- `compose.homologacao.yml`
- `.env.homologacao.example` (o arquivo de execução `.env.homologacao` é local/ignorado)
- `tests/homologacao/keycloak/hidro-homologacao-realm.json`
- `tests/homologacao/seed.sql`
- `tests/homologacao/run_http_tests.py`
- `tests/HidroCore.Homologacao/` (harness de integração PostgreSQL)

## Pendências

- Caso seja necessária validação de CRUD via HTTP, implementar endpoints de domínio; essa API ainda não os possui.
- Os containers e redes exclusivos do projeto foram removidos ao final; os volumes `hidrocore-homologacao-20261102_pgdata` e `..._keycloakdata` foram preservados. O Compose de outro projeto continuou em execução, sem alterações. As imagens locais com prefixo `hidrocore-homologacao-20261102-` permanecem no cache Docker.


## Continuação — API HTTP REST multi-tenant

**Resultado: PASS.** Implementados CRUD de usinas, estações e equipamentos; consulta de grandezas e leituras; filtros por estação/grandeza/período; paginação; e gravação de leituras com validação e proteção contra duplicidade. Contratos estão em `docs/api-http.md`; OpenAPI local expõe 12 caminhos.

- Build da solução e imagens API/Web/Worker: PASS, sem warnings/erros no build .NET.
- xUnit: PASS, 19/19.
- Testes HTTP reais com Keycloak JWT e PostgreSQL preservado: PASS — `tests/homologacao/run_http_tests.py --check`.
- Validado: papéis Administrador/Operador, 401/403, tenant por domínio, bloqueio de Host/encaminhamento forjado, CRUD completo, refs cross-tenant, duplicidade, precisão, filtros/período/paginação, 404 cruzado e 409 ao excluir entidades vinculadas.
- TenantId é derivado exclusivamente do contexto de domínio; IDs cruzados não são encontrados pelas consultas filtradas. Operador pode gravar leituras, mas não administrar cadastros.
- Nenhuma migration nova foi necessária ou executada: o schema versionado já tinha as tabelas, FKs compostas e índice único necessários.
- Os volumes PostgreSQL e Keycloak existentes foram reutilizados e preservados. Nenhum container/volume de outro projeto foi alterado ou removido.
- Worker continua apenas planejando ANA/CCEE, ligado somente à rede Docker interna.

A stack de homologação está ativa localmente em API `127.0.0.1:18090` e Web `127.0.0.1:18091`, com Keycloak `127.0.0.1:18080`; o PostgreSQL não tem porta publicada no host. Os containers/volumes permanecem disponíveis para inspeção local.
