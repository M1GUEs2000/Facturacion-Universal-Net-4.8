/* ============================================================================
   schema.sql — Facturación Universal .NET 4.8
   Motor: SQL Server  |  BD: FacturacionCore (92.204.64.145)

   Reescritura del schema PostgreSQL de [[facturacion-universal]] adaptado a
   SQL Server. Alcance MVP: SOLO facturas (NC y Retenciones son post-MVP).

   Diferencias vs el origen .NET 8:
   - PKs INT IDENTITY (las entidades usan `int Id`), no UUID
   - facturas_detalle simplificado: sin ice_codigo/ice_tarifa/ice_base ni
     iva_codigo string / iva_tarifa — solo codigo_iva, iva_base, iva_valor, ice_valor
   - storage en filesystem local (se persiste solo la ruta)

   CONVENCIÓN DAPPER (para P-005): las columnas usan snake_case. El repositorio
   debe activar  Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true  para
   que mapeen a las propiedades PascalCase de las entidades.

   Enums:
   - estado_sri se guarda como VARCHAR(30) (legible para el índice filtrado):
     'PENDIENTE' | 'ENVIADO' | 'PENDIENTE_AUTORIZACION' |
     'AUTORIZADO_PENDIENTE_ARCHIVOS' | 'AUTORIZADO' | 'NO_AUTORIZADO' | 'ANULADO'
   - ambiente, tipo_identificacion_comprador, tipo_documento, codigo_iva se
     guardan como INT (el valor del enum) — Dapper los mapea automáticamente.

   Script idempotente: cada objeto se crea solo si no existe.
   ============================================================================ */

-- QUOTED_IDENTIFIER/ANSI_NULLS deben ir ON para crear el índice filtrado UX_secuencial_activo
-- (sqlcmd los pone OFF por defecto y CREATE INDEX filtrado falla).
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
GO

/* ─── empresas ───────────────────────────────────────────────────────────── */
IF OBJECT_ID(N'dbo.empresas', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.empresas (
        id                    INT            IDENTITY(1,1) NOT NULL,
        ruc                   VARCHAR(13)    NOT NULL,
        razon_social          NVARCHAR(300)  NOT NULL,
        nombre_comercial      NVARCHAR(300)  NULL,
        dir_matriz            NVARCHAR(300)  NOT NULL,
        obligado_contabilidad BIT            NOT NULL CONSTRAINT DF_empresas_oblcont DEFAULT (0),
        -- cert_password: en producción debe almacenarse cifrado AES-256-GCM (ver P-013 hardening)
        certificado_path      NVARCHAR(500)  NOT NULL,
        cert_password         NVARCHAR(500)  NOT NULL,
        created_at            DATETIME2(3)   NOT NULL CONSTRAINT DF_empresas_created DEFAULT (SYSUTCDATETIME()),
        updated_at            DATETIME2(3)   NOT NULL CONSTRAINT DF_empresas_updated DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_empresas      PRIMARY KEY (id),
        CONSTRAINT UQ_empresas_ruc  UNIQUE (ruc)
    );
END
GO

/* ─── parametros_facturacion ─────────────────────────────────────────────── */
IF OBJECT_ID(N'dbo.parametros_facturacion', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.parametros_facturacion (
        id                         INT           IDENTITY(1,1) NOT NULL,
        empresa_ruc                VARCHAR(13)   NOT NULL,
        estab                      VARCHAR(3)    NOT NULL,
        pto_emi                    VARCHAR(3)    NOT NULL,
        direccion_establecimiento  NVARCHAR(300) NULL,
        contribuyente_especial     VARCHAR(13)   NULL,
        created_at                 DATETIME2(3)  NOT NULL CONSTRAINT DF_parametros_created DEFAULT (SYSUTCDATETIME()),
        updated_at                 DATETIME2(3)  NOT NULL CONSTRAINT DF_parametros_updated DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_parametros_facturacion PRIMARY KEY (id),
        CONSTRAINT UQ_parametros_empresa     UNIQUE (empresa_ruc),
        CONSTRAINT FK_parametros_empresa     FOREIGN KEY (empresa_ruc) REFERENCES dbo.empresas (ruc)
    );
END
GO

/* ─── secuenciales_sri ───────────────────────────────────────────────────── */
IF OBJECT_ID(N'dbo.secuenciales_sri', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.secuenciales_sri (
        id                INT          IDENTITY(1,1) NOT NULL,
        empresa_ruc       VARCHAR(13)  NOT NULL,
        estab             VARCHAR(3)   NOT NULL,
        pto_emi           VARCHAR(3)   NOT NULL,
        tipo_documento    INT          NOT NULL,  -- 1=Factura, 4=NotaCredito, 7=Retencion
        ultimo_secuencial INT          NOT NULL CONSTRAINT DF_secuenciales_ultimo DEFAULT (0),
        created_at        DATETIME2(3) NOT NULL CONSTRAINT DF_secuenciales_created DEFAULT (SYSUTCDATETIME()),
        updated_at        DATETIME2(3) NOT NULL CONSTRAINT DF_secuenciales_updated DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_secuenciales_sri  PRIMARY KEY (id),
        -- garantiza atomicidad de IncrementarYObtenerAsync (UPDATE ... OUTPUT)
        CONSTRAINT UQ_secuencial_unico  UNIQUE (empresa_ruc, estab, pto_emi, tipo_documento),
        CONSTRAINT FK_secuenciales_empresa FOREIGN KEY (empresa_ruc) REFERENCES dbo.empresas (ruc)
    );
END
GO

/* ─── facturas ───────────────────────────────────────────────────────────── */
IF OBJECT_ID(N'dbo.facturas', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.facturas (
        id                            INT            IDENTITY(1,1) NOT NULL,
        empresa_ruc                   VARCHAR(13)    NOT NULL,
        clave_acceso                  VARCHAR(49)    NOT NULL,
        ambiente                      INT            NOT NULL,  -- 1=Pruebas, 2=Produccion
        estab                         VARCHAR(3)     NOT NULL,
        pto_emi                       VARCHAR(3)     NOT NULL,
        secuencial                    VARCHAR(9)     NOT NULL,
        fecha_emision                 DATE           NOT NULL,
        estado_sri                    VARCHAR(30)    NOT NULL,
        -- comprador (embebido — inmutabilidad fiscal)
        tipo_identificacion_comprador INT            NOT NULL,  -- 4=RUC,5=Cedula,6=Pasaporte,7=ConsumidorFinal,8=Exterior,9=Placa
        identificacion_comprador      VARCHAR(20)    NOT NULL,
        razon_social_comprador        NVARCHAR(300)  NOT NULL,
        direccion_comprador           NVARCHAR(300)  NULL,
        guia_remision                 VARCHAR(20)    NULL,
        -- totales
        total_sin_impuestos           DECIMAL(18,6)  NOT NULL,
        total_descuento               DECIMAL(18,6)  NOT NULL CONSTRAINT DF_facturas_totdesc DEFAULT (0),
        total_iva                     DECIMAL(18,6)  NOT NULL CONSTRAINT DF_facturas_totiva  DEFAULT (0),
        total_ice                     DECIMAL(18,6)  NOT NULL CONSTRAINT DF_facturas_totice  DEFAULT (0),
        importe_total                 DECIMAL(18,6)  NOT NULL,
        moneda                        VARCHAR(10)    NOT NULL CONSTRAINT DF_facturas_moneda  DEFAULT ('DOLAR'),
        -- colecciones embebidas como JSON
        formas_pago                   NVARCHAR(MAX)  NULL,  -- [{codigo,total,plazo?,unidadTiempo?}]
        info_adicional                NVARCHAR(MAX)  NULL,  -- [{nombre,valor}]
        -- respuesta SRI + archivos
        numero_autorizacion           VARCHAR(49)    NULL,
        fecha_autorizacion            DATETIMEOFFSET NULL,
        sri_respuesta                 NVARCHAR(MAX)  NULL,
        xml_firmado_path              NVARCHAR(500)  NULL,
        xml_autorizado_path           NVARCHAR(500)  NULL,
        pdf_path                      NVARCHAR(500)  NULL,
        created_at                    DATETIME2(3)   NOT NULL CONSTRAINT DF_facturas_created DEFAULT (SYSUTCDATETIME()),
        updated_at                    DATETIME2(3)   NOT NULL CONSTRAINT DF_facturas_updated DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_facturas             PRIMARY KEY (id),
        CONSTRAINT UQ_facturas_claveacceso UNIQUE (clave_acceso),
        CONSTRAINT FK_facturas_empresa     FOREIGN KEY (empresa_ruc) REFERENCES dbo.empresas (ruc)
    );
END
GO

/* índice de búsqueda frecuente */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_facturas_busqueda' AND object_id = OBJECT_ID(N'dbo.facturas'))
BEGIN
    CREATE INDEX IX_facturas_busqueda
        ON dbo.facturas (empresa_ruc, estado_sri, fecha_emision);
END
GO

/* índice filtrado: un único secuencial ACTIVO por (empresa, estab, pto, secuencial, ambiente).
   PENDIENTE y NO_AUTORIZADO NO bloquean el secuencial → quedan fuera del índice.
   SQL Server no admite NOT IN en filtros de índice → se usa <> AND <>. */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_secuencial_activo' AND object_id = OBJECT_ID(N'dbo.facturas'))
BEGIN
    CREATE UNIQUE INDEX UX_secuencial_activo
        ON dbo.facturas (empresa_ruc, estab, pto_emi, secuencial, ambiente)
        WHERE estado_sri <> 'PENDIENTE' AND estado_sri <> 'NO_AUTORIZADO';
END
GO

/* ─── facturas_detalle ───────────────────────────────────────────────────── */
IF OBJECT_ID(N'dbo.facturas_detalle', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.facturas_detalle (
        id                        INT           IDENTITY(1,1) NOT NULL,
        factura_id                INT           NOT NULL,
        codigo_principal          VARCHAR(25)   NOT NULL,
        codigo_auxiliar           VARCHAR(25)   NULL,
        descripcion               NVARCHAR(300) NOT NULL,
        cantidad                  DECIMAL(18,6) NOT NULL,
        precio_unitario           DECIMAL(18,6) NOT NULL,
        descuento                 DECIMAL(18,6) NOT NULL CONSTRAINT DF_detalle_descuento DEFAULT (0),
        precio_total_sin_impuesto DECIMAL(18,6) NOT NULL,
        codigo_iva                INT           NOT NULL,  -- 0,2,3,4,5,6,7,8,10 (catálogo IVA SRI)
        iva_base                  DECIMAL(18,6) NOT NULL,  -- precio_total_sin_impuesto + ice_valor
        iva_valor                 DECIMAL(18,6) NOT NULL CONSTRAINT DF_detalle_ivavalor DEFAULT (0),
        ice_valor                 DECIMAL(18,6) NOT NULL CONSTRAINT DF_detalle_icevalor DEFAULT (0),
        CONSTRAINT PK_facturas_detalle  PRIMARY KEY (id),
        CONSTRAINT FK_detalle_factura   FOREIGN KEY (factura_id)
            REFERENCES dbo.facturas (id) ON DELETE CASCADE
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_detalle_factura' AND object_id = OBJECT_ID(N'dbo.facturas_detalle'))
BEGIN
    CREATE INDEX IX_detalle_factura
        ON dbo.facturas_detalle (factura_id);
END
GO
