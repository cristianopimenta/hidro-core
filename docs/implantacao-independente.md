# Implantação independente por usina

## Modelo

Cada usina recebe uma VPS Ubuntu, um checkout da mesma versão do Hidro-Core, um domínio e um projeto Docker Compose próprios. A configuração comum está em `compose.producao.yml`, `deploy/nginx/default.conf.template` e `deploy/postgres/01-create-keycloak-db.sh`. A configuração específica fica em um arquivo `.env` privado por instalação (use `deploy/usina.example.env` como modelo). `deploy/common.env.example` documenta valores comuns não secretos. Não copie os segredos do arquivo de exemplo; gere valores novos para cada instalação.

PostgreSQL, Keycloak e Nginx têm verificações de prontidão/configuração; API, Web e Worker usam verificação de processo para evitar depender de ferramentas extras nas imagens runtime. Monitore também `/health` da API por HTTPS e alertas/logs: a verificação do container da API não valida dependências nem consulta o endpoint HTTP.

O nome exclusivo em `COMPOSE_PROJECT_NAME` cria volumes e rede independentes. PostgreSQL guarda o banco Hidro-Core e um banco/usuário separado do Keycloak no mesmo servidor PostgreSQL; Keycloak persiste no volume PostgreSQL. A conta bootstrap do PostgreSQL não é usada pelos serviços: API/Worker usam uma conta runtime proprietária apenas do banco Hidro-Core, e Keycloak usa outra conta. Nginx é o único serviço com portas publicadas. API, Web, Worker e PostgreSQL/Keycloak ficam na rede interna do Compose. O certificado TLS é provisionado e renovado pelo procedimento operacional da VPS e montado como somente leitura em Nginx.

O middleware atual da API resolve o tenant pelo `Host`; registre o hostname público como domínio ativo no banco. A API recebe o issuer OIDC público `https://<domínio>/auth/realms/<realm>`, que deve coincidir com o issuer emitido pelo Keycloak. O caminho público `/api/...` conserva o prefixo que os endpoints atuais usam. Nginx não habilita cabeçalhos de host arbitrários, e a API não confia em `X-Forwarded-Host`.

## Provisionar nova usina

1. Provisionar uma VPS Ubuntu suportada, Docker Engine/Compose, DNS do domínio para a VPS, firewall permitindo apenas SSH administrativo e HTTP/HTTPS necessários, e certificado TLS válido. Este documento não provisiona recursos externos.
2. Obter o código na versão aprovada e conferir a assinatura/hash do artefato conforme o processo da equipe. Criar `deploy/usinas/<slug>/.env` a partir de `deploy/usina.example.env`; preencher domínio, slug, banco, realm e gerar senhas fortes, únicas e distintas. Guardar o arquivo e cópias de segurança fora do Git, com permissões restritas (`chmod 600`).
3. Ajustar as opções comuns desejadas a partir de `deploy/common.env.example`. Passe os dois arquivos ao Compose em ordem, com o específico por último: `docker compose --env-file deploy/common.env.example --env-file deploy/usinas/<slug>/.env -p <slug> -f compose.producao.yml config`. Revise a saída localmente sem compartilhá-la (ela contém valores interpolados).
4. Preparar backup externo seguro dos segredos e o destino de backup criptografado do PostgreSQL. Confirmar o diretório do certificado apontado por `TLS_CERTIFICATE_DIR` e os nomes dos arquivos.
5. Iniciar a stack: `docker compose --env-file deploy/common.env.example --env-file deploy/usinas/<slug>/.env -p <slug> -f compose.producao.yml up -d --build`. O comando é documentado, não foi executado nesta etapa.
6. Aplicar manualmente, após revisão, a migration SQL versionada; não há migration automática no startup. Exemplo via stdin como usuário proprietário da aplicação: `docker compose --env-file deploy/common.env.example --env-file deploy/usinas/<slug>/.env -p <slug> -f compose.producao.yml exec -T postgres sh -ec 'exec psql -U "$APP_DB_USER" -d "$POSTGRES_DB" -v ON_ERROR_STOP=1' < src/HidroCore.Infrastructure/Migrations/0001_initial_multitenant.sql`. Valide primeiro que o alvo é a instalação nova. Não use em banco com schema existente sem plano de migração revisado.
7. Configurar realm e cliente OIDC no Keycloak: issuer público, audience igual a `API_AUDIENCE`, claims `sub` e `tenant_id`, usuários, papéis e políticas de senha/MFA conforme política local. Criar os registros iniciais de tenant/domínio/usuário por procedimento administrativo controlado; ainda não existe API pública de provisionamento.
8. Cadastrar usinas, estações, equipamentos, grandezas e leituras pelos endpoints documentados em `api-http.md`; validar `/health`, `/auth/realms/<realm>/.well-known/openid-configuration`, HTTPS, autenticação e acesso restrito ao hostname provisionado.

O usuário runtime do app é criado e torna-se dono do banco da aplicação pelo script de inicialização do Postgres. `KEYCLOAK_DB` e seu usuário também são criados pelo script somente quando o volume é vazio. Ao mudar as variáveis depois de inicializar, o script não recria nem altera credenciais existentes. Para volumes existentes, qualquer ajuste de usuários deve ser uma operação PostgreSQL revisada e independente.

## Migrations e atualização

Não execute migrations no entrypoint. Para cada release, revisar SQL e compatibilidade, registrar backup válido, janela e plano de retorno. Testar upgrade e restauração em cópia isolada. Aplicar a migration uma única vez ao banco alvo com `psql -v ON_ERROR_STOP=1`; confirmar `schema_migrations` e smoke tests antes de atualizar tráfego. O arquivo 0001 é baseline inicial, não script idempotente para reaplicar.

Atualização recomendada: colocar a instalação em manutenção; criar e verificar backup; obter a nova versão; avaliar diferenças de configuração e de banco; aplicar migrations compatíveis primeiro; construir/subir com `docker compose ... up -d --build`; verificar saúde de serviços, logs e fluxos; liberar tráfego. Fixe tags de imagens e versione a revisão da aplicação. Não use `down -v`.

## Backup, restauração e rollback

Use dumps consistentes por banco, com criptografia em trânsito e repouso, acesso restrito e retenção definida. Exemplo de dump lógico do banco da aplicação: `docker compose --env-file deploy/common.env.example --env-file deploy/usinas/<slug>/.env -p <slug> -f compose.producao.yml exec -T postgres sh -ec 'exec pg_dump -U "$APP_DB_USER" -d "$POSTGRES_DB" -Fc' > hidrocore-<data>.dump`; faça dump separado do banco Keycloak usando o usuário de serviço apropriado ou ferramenta com privilégios de backup. Armazene fora da VPS e teste restauração periodicamente em ambiente isolado. Um backup do diretório de volume a frio também pode complementar, com serviços parados e procedimento consistente.

Para restaurar, parar os serviços que escrevem, restaurar bancos em uma instância isolada ou banco vazio com `pg_restore`, validar permissões e consistência, e então iniciar a aplicação. Para rollback de release, volte à imagem/revisão anterior somente se o schema for compatível. Migrations destrutivas exigem estratégia expand/contract e não são revertidas por troca de imagem; restauração do backup é a opção de retorno de dados e pode perder gravações posteriores ao backup.

## Conectores e ANA/CCEE

Os dados atuais de `integracoes_usina` já permitem configuração por usina (módulo, JSON, agendas). A instalação isolada também permite segredos e endpoints próprios por usina em implantação futura. Nesta etapa, não há transmissão real, segredos de integração configurados nem ativação de ANA/CCEE: `Worker__ExternalDispatchEnabled=false`, `ANA_ENABLED=false`, `CCEE_ENABLED=false` no template. O código registra módulos documentais sem protocolo implementado. Um conector futuro deve definir contrato, armazenamento seguro de segredo, allowlist de saída, retry/idempotência, observabilidade e autorização operacional antes de qualquer ativação.

## Homologação

`compose.homologacao.yml`, `.env.homologacao`, seus volumes, portas e projeto continuam independentes. Não use o nome do projeto de homologação no Modelo A. Nenhum comando desta documentação deve ser apontado à homologação sem intenção específica.
