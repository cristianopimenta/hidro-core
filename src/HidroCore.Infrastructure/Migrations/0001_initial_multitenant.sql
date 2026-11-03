-- Version 0001: baseline multi-tenant schema. Apply explicitly after review; never at app startup.
CREATE TABLE tenants (
    "Id" uuid PRIMARY KEY,
    "Nome" varchar(160) NOT NULL,
    "Chave" varchar(100) NOT NULL UNIQUE,
    "Ativo" boolean NOT NULL,
    "CorPrimaria" varchar(7) NOT NULL,
    "LogoUrl" varchar(2048)
);
CREATE TABLE tenant_dominios (
    "Hostname" varchar(253) PRIMARY KEY,
    "TenantId" uuid NOT NULL REFERENCES tenants("Id") ON DELETE RESTRICT,
    "Ativo" boolean NOT NULL
);
CREATE INDEX ix_tenant_dominios_tenant ON tenant_dominios("TenantId");
CREATE TABLE tenant_usuarios (
    "TenantId" uuid NOT NULL REFERENCES tenants("Id") ON DELETE CASCADE,
    "IdentitySubject" varchar(200) NOT NULL,
    "Email" varchar(320) NOT NULL,
    "Ativo" boolean NOT NULL,
    "Papel" varchar(60) NOT NULL,
    PRIMARY KEY ("TenantId", "IdentitySubject"),
    UNIQUE ("TenantId", "Email")
);
CREATE TABLE usinas (
    "Id" uuid PRIMARY KEY,
    "TenantId" uuid NOT NULL REFERENCES tenants("Id"),
    "Nome" varchar(160) NOT NULL,
    "Codigo" varchar(80) NOT NULL,
    UNIQUE ("TenantId", "Id"),
    UNIQUE ("TenantId", "Codigo")
);
CREATE TABLE estacoes (
    "Id" uuid PRIMARY KEY,
    "TenantId" uuid NOT NULL,
    "UsinaId" uuid NOT NULL,
    "Nome" varchar(160) NOT NULL,
    "Codigo" varchar(80) NOT NULL,
    UNIQUE ("TenantId", "Id"),
    UNIQUE ("TenantId", "UsinaId", "Codigo"),
    FOREIGN KEY ("TenantId", "UsinaId") REFERENCES usinas("TenantId", "Id")
);
CREATE TABLE equipamentos (
    "Id" uuid PRIMARY KEY,
    "TenantId" uuid NOT NULL,
    "EstacaoId" uuid NOT NULL,
    "Nome" varchar(160) NOT NULL,
    "Codigo" varchar(100) NOT NULL,
    UNIQUE ("TenantId", "Id"),
    UNIQUE ("TenantId", "EstacaoId", "Codigo"),
    FOREIGN KEY ("TenantId", "EstacaoId") REFERENCES estacoes("TenantId", "Id")
);
CREATE TABLE grandezas (
    "Id" uuid PRIMARY KEY,
    "TenantId" uuid NOT NULL REFERENCES tenants("Id"),
    "Nome" varchar(120) NOT NULL,
    "Unidade" varchar(40) NOT NULL,
    UNIQUE ("TenantId", "Id"),
    UNIQUE ("TenantId", "Nome")
);
CREATE TABLE leituras (
    "Id" uuid PRIMARY KEY,
    "TenantId" uuid NOT NULL,
    "EquipamentoId" uuid NOT NULL,
    "GrandezaId" uuid NOT NULL,
    "Instante" timestamptz NOT NULL,
    "Valor" numeric(20,6) NOT NULL,
    UNIQUE ("TenantId", "EquipamentoId", "GrandezaId", "Instante"),
    FOREIGN KEY ("TenantId", "EquipamentoId") REFERENCES equipamentos("TenantId", "Id"),
    FOREIGN KEY ("TenantId", "GrandezaId") REFERENCES grandezas("TenantId", "Id")
);
CREATE INDEX ix_leituras_tenant_instante ON leituras("TenantId", "Instante");
CREATE TABLE integracoes_usina (
    "Id" uuid PRIMARY KEY,
    "TenantId" uuid NOT NULL,
    "UsinaId" uuid NOT NULL,
    "Modulo" varchar(80) NOT NULL,
    "ConfiguracaoJson" jsonb NOT NULL DEFAULT '{}'::jsonb,
    "CronColeta" varchar(100),
    "CronTransmissao" varchar(100),
    "Habilitada" boolean NOT NULL,
    FOREIGN KEY ("TenantId", "UsinaId") REFERENCES usinas("TenantId", "Id")
);
CREATE INDEX ix_integracoes_usina_tenant_schedule ON integracoes_usina("TenantId", "Habilitada");

CREATE TABLE schema_migrations (
    "Version" varchar(80) PRIMARY KEY,
    "AppliedAt" timestamptz NOT NULL DEFAULT now()
);
INSERT INTO schema_migrations ("Version") VALUES ('0001_initial_multitenant');
