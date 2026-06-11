PLAN MIGRACION NET 4.8 

Plan completo — FacturacionCore en .NET 4.8
Estructura de la solución

d:\Proyectos\FacturacionCore\
├── FacturacionCore.sln
├── Facturacion.Core\           ← dominio puro, sin dependencias externas
├── Facturacion.Infraestructura\← Dapper, iText7, FirmaXadesNet, SRI
├── Facturacion.Api\            ← WebAPI 2, JWT, FluentValidation
└── Facturacion.Tests\          ← xUnit, FluentAssertions, NSubstitute
DÍA 1 — Core completo + Infraestructura base (6h)
H1 — Scaffold de la solución

# Crear solución y proyectos SDK-style (C# 12 en .NET 4.8)
dotnet new sln -n FacturacionCore -o d:\Proyectos\FacturacionCore
cd d:\Proyectos\FacturacionCore
dotnet new classlib -n Facturacion.Core --framework net48
dotnet new classlib -n Facturacion.Infraestructura --framework net48
dotnet new webapi  -n Facturacion.Api --framework net48   # luego switch a WebAPI 2
dotnet new xunit   -n Facturacion.Tests --framework net48
dotnet sln add **/*.csproj
NuGet por proyecto:

Proyecto	Paquetes
Core	ErrorOr 1.3.x
Infraestructura	Dapper · Microsoft.Data.SqlClient 5.x · FirmaXadesNetCore · itext7 8.x · Serilog · Microsoft.Extensions.Http 8.x · System.Text.Json 8.x
Api	Microsoft.AspNet.WebApi 5.3 · FluentValidation 11.x · System.IdentityModel.Tokens.Jwt 8.x · Microsoft.Extensions.DependencyInjection 8.x · Serilog.AspNetCore
Tests	xunit · FluentAssertions · NSubstitute
H1-H3 — Facturacion.Core (copy-paste inteligente de FU)
Archivos a crear — todos salen de FU con mínimas adaptaciones:


Facturacion.Core/
├── Enums/
│   ├── EstadoSri.cs          ← 7 valores: Pendiente→Autorizado + checkpoints
│   ├── Ambiente.cs
│   └── CodigoIva.cs          ← 0,2,3,4,5,6,7,8,10
├── Entidades/
│   ├── DocumentoElectronico.cs   ← clase base abstracta con 18 props + 8 mutaciones
│   ├── Factura.cs                ← hereda DocumentoElectronico
│   ├── FacturaDetalle.cs
│   ├── Empresa.cs
│   ├── SecuencialSri.cs
│   └── ParametrosFacturacion.cs
├── Interfaces/
│   ├── IDocumentoEmitible.cs     ← checkpoints como interfaz
│   ├── IFacturasRepositorio.cs   ← incluye ExisteSecuencialActivoAsync
│   ├── IEmpresasRepositorio.cs
│   ├── ISecuencialesRepositorio.cs  ← incluye IncrementarYObtenerAsync
│   ├── IParametrosRepositorio.cs
│   ├── IServicioXml.cs
│   ├── IServicioFirma.cs
│   ├── IServicioSri.cs
│   └── IServicioPdf.cs
├── CasosDeUso/
│   ├── Comun/
│   │   ├── OrquestadorEmision.cs    ← 5 checkpoints, copy de FU
│   │   └── OrquestadorReintento.cs  ← salta pasos ya hechos, copy de FU
│   └── Facturas/
│       ├── EmitirFactura.cs
│       └── ReintentarEmisionFactura.cs
├── Metodos/
│   └── GeneradorClaveAcceso.cs  ← módulo 11, copy de FU
└── Errores.cs                   ← todos los errores tipados con ErrorOr
Piezas críticas del Core:

EstadoSri.cs — los 7 estados que son la columna vertebral del sistema:


public enum EstadoSri
{
    Pendiente,                  // INSERT hecho, aún no firmado
    PendienteAutorizacion,      // enviado al SRI, esperando
    AutorizadoPendienteArchivos,// SRI autorizó, guardando XML+PDF
    Autorizado,                 // flujo completo
    NoAutorizado,               // SRI rechazó — secuencial libre
    Anulado
}
IDocumentoEmitible.cs — lo que el OrquestadorEmision necesita de cualquier comprobante:


public interface IDocumentoEmitible
{
    EstadoSri EstadoSri { get; }
    string? XmlFirmadoPath { get; }
    string? XmlAutorizadoPath { get; }
    string? PdfPath { get; }
    string? NumeroAutorizacion { get; }
    void RegistrarXmlFirmado(string path);
    void RegistrarEnvioSri();
    void RegistrarNumeroAutorizacion(string numero, DateTimeOffset fecha, string? sriRespuesta = null);
    void RegistrarAutorizacionSri(string numero, DateTimeOffset fecha, string xmlPath, string? sriRespuesta = null);
    void RegistrarNoAutorizacion(string? sriRespuesta = null);
    void RegistrarPdf(string path);
}
ISecuencialesRepositorio.cs — el método que elimina el race condition:


public interface ISecuencialesRepositorio
{
    Task<long> IncrementarYObtenerAsync(string empresaRuc, string tipoComprobante, CancellationToken ct);
    Task<bool> ExisteAsync(string empresaRuc, string tipoComprobante, CancellationToken ct);
}
H4-H6 — Infraestructura base
ServicioXml.cs — copy de FU, XmlSerializer con ShouldSerialize*(). Las reglas críticas que ya sabemos:

ICE antes que IVA en <totalConImpuestos> e <impuestos> (orden obligatorio SRI)
guiaRemision antes de razonSocialComprador en <infoFactura>
moneda después de importeTotal
precioUnitario con 6 decimales, resto con 2
XmlWriter con UTF8Encoding(false) — sin BOM
ServicioFirma.cs — wrappea FirmadorNativo de SF (copiar el archivo al nuevo proyecto):


public class ServicioFirma : IServicioFirma
{
    public ErrorOr<string> Firmar(byte[] xmlBytes, byte[] p12Bytes, string p12Password)
    {
        // FirmadorNativo.Sign() — ya validado en SF
    }
}
DÍA 2 — Repositorios + SRI + PDF + API (6h)
H1-H2 — Schema SQL Server + Repositorios Dapper
Script SQL Server completo — schema.sql:


CREATE DATABASE FacturacionCore
GO
USE FacturacionCore
GO

CREATE TABLE empresas (
    ruc             VARCHAR(13)      PRIMARY KEY,
    nombre          NVARCHAR(300)    NOT NULL,
    dir_matriz      NVARCHAR(300)    NOT NULL,
    nombre_comercial NVARCHAR(300)   NULL,
    certificado_path NVARCHAR(500)   NULL,
    cert_password   NVARCHAR(500)    NULL,  -- AES-256 cifrado
    logo_path       NVARCHAR(500)    NULL,
    created_at      DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    updated_at      DATETIME2        NOT NULL DEFAULT GETUTCDATE()
)
GO

CREATE TABLE parametros_facturacion (
    empresa_ruc          VARCHAR(13)  PRIMARY KEY REFERENCES empresas(ruc),
    ambiente             VARCHAR(1)   NOT NULL,   -- '1' pruebas / '2' produccion
    tipo_emision         VARCHAR(1)   NOT NULL DEFAULT '1',
    agente_retencion     BIT          NOT NULL DEFAULT 0,
    contribuyente_rimpe  NVARCHAR(100) NULL,
    estab                VARCHAR(3)   NOT NULL,
    punto_emision        VARCHAR(3)   NOT NULL,
    contribuyente_especial VARCHAR(13) NULL,
    obligado_contabilidad BIT         NOT NULL DEFAULT 0,
    codigo_porcentaje    INT          NOT NULL DEFAULT 4,  -- IVA 15%
    updated_at           DATETIME2    NOT NULL DEFAULT GETUTCDATE()
)
GO

CREATE TABLE secuenciales_sri (
    id               UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    empresa_ruc      VARCHAR(13)  NOT NULL REFERENCES empresas(ruc),
    tipo_comprobante VARCHAR(2)   NOT NULL,  -- '01' factura
    secuencial       BIGINT       NOT NULL DEFAULT 0,
    codigo_numerico  VARCHAR(8)   NOT NULL,
    updated_at       DATETIME2    NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT UQ_secuencial UNIQUE (empresa_ruc, tipo_comprobante)
)
GO

CREATE TABLE facturas (
    id                              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    empresa_ruc                     VARCHAR(13)    NOT NULL REFERENCES empresas(ruc),
    ip_address                      VARCHAR(45)    NOT NULL,
    ambiente                        VARCHAR(1)     NOT NULL,
    estab                           VARCHAR(3)     NOT NULL,
    pto_emi                         VARCHAR(3)     NOT NULL,
    secuencial                      VARCHAR(9)     NOT NULL,
    clave_acceso                    VARCHAR(49)    NOT NULL,
    fecha_emision                   DATE           NOT NULL,
    tipo_identificacion_comprador   VARCHAR(2)     NOT NULL,
    identificacion_comprador        VARCHAR(20)    NOT NULL,
    razon_social_comprador          NVARCHAR(300)  NOT NULL,
    direccion_comprador             NVARCHAR(300)  NULL,
    dir_establecimiento             NVARCHAR(300)  NULL,
    total_sin_impuestos             DECIMAL(12,2)  NOT NULL,
    total_descuento                 DECIMAL(12,2)  NOT NULL DEFAULT 0,
    base_imponible_ice              DECIMAL(12,2)  NULL,
    valor_ice                       DECIMAL(12,2)  NULL,
    base_imponible_iva              DECIMAL(12,2)  NOT NULL,
    valor_iva                       DECIMAL(12,2)  NOT NULL,
    importe_total                   DECIMAL(12,2)  NOT NULL,
    formas_pago                     NVARCHAR(MAX)  NOT NULL,  -- JSON
    info_adicional                  NVARCHAR(MAX)  NULL,      -- JSON
    estado_sri                      VARCHAR(30)    NOT NULL DEFAULT 'PENDIENTE',
    numero_autorizacion             VARCHAR(49)    NULL,
    fecha_autorizacion              DATETIME2      NULL,
    sri_respuesta                   NVARCHAR(MAX)  NULL,
    xml_firmado_path                NVARCHAR(500)  NULL,
    xml_autorizado_path             NVARCHAR(500)  NULL,
    pdf_path                        NVARCHAR(500)  NULL,
    created_at                      DATETIME2      NOT NULL DEFAULT GETUTCDATE(),
    updated_at                      DATETIME2      NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT UQ_clave_acceso UNIQUE (clave_acceso)
)
GO

-- Índice filtrado (equivalente al índice parcial de PostgreSQL de FU)
CREATE UNIQUE INDEX UX_secuencial_activo
ON facturas (empresa_ruc, estab, pto_emi, secuencial, ambiente)
WHERE estado_sri NOT IN ('PENDIENTE', 'NO_AUTORIZADO')
GO

CREATE INDEX IX_facturas_busqueda ON facturas (empresa_ruc, estado_sri, fecha_emision)
GO

CREATE TABLE facturas_detalle (
    id                          UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    factura_id                  UNIQUEIDENTIFIER NOT NULL REFERENCES facturas(id) ON DELETE CASCADE,
    orden                       INT              NOT NULL,
    codigo_principal            VARCHAR(25)      NOT NULL,
    codigo_auxiliar             VARCHAR(25)      NULL,
    descripcion                 NVARCHAR(300)    NOT NULL,
    cantidad                    DECIMAL(12,6)    NOT NULL,
    precio_unitario             DECIMAL(12,6)    NOT NULL,
    descuento                   DECIMAL(12,2)    NOT NULL DEFAULT 0,
    precio_total_sin_impuesto   DECIMAL(12,2)    NOT NULL,
    ice_codigo                  VARCHAR(10)      NULL,
    ice_tarifa                  DECIMAL(5,2)     NULL,
    ice_base                    DECIMAL(12,2)    NULL,
    ice_valor                   DECIMAL(12,2)    NULL,
    iva_codigo                  INT              NOT NULL,
    iva_tarifa                  DECIMAL(5,2)     NOT NULL,
    iva_base                    DECIMAL(12,2)    NOT NULL,
    iva_valor                   DECIMAL(12,2)    NOT NULL
)
GO

CREATE TABLE logs (
    id          UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    empresa_ruc VARCHAR(13)    NULL,
    nivel       VARCHAR(10)    NOT NULL,
    mensaje     NVARCHAR(MAX)  NOT NULL,
    detalle     NVARCHAR(MAX)  NULL,
    created_at  DATETIME2      NOT NULL DEFAULT GETUTCDATE()
)
GO
Secuencial atómico en SQL Server — el UPDATE con OUTPUT que elimina el race condition:


UPDATE secuenciales_sri
SET secuencial = secuencial + 1, updated_at = GETUTCDATE()
OUTPUT INSERTED.secuencial
WHERE empresa_ruc = @empresaRuc AND tipo_comprobante = @tipoComprobante
FacturasRepositorio.cs — los métodos críticos:


// INSERT antes de enviar al SRI — el documento siempre existe en BD
Task AgregarAsync(Factura factura, CancellationToken ct);

// UPDATE por checkpoint — llamado desde OrquestadorEmision
Task PersistirAsync(Factura factura, CancellationToken ct);

// Para OrquestadorReintento
Task<Factura?> ObtenerPorIdAsync(Guid id, CancellationToken ct);

// Índice filtrado garantiza unicidad a nivel BD
Task<bool> ExisteSecuencialActivoAsync(
    string empresaRuc, string estab, string ptoEmi,
    string secuencial, string ambiente, CancellationToken ct);
H3 — ServicioSri.cs
Copy de FU, solo HttpClient — no hay dependencia de ASP.NET Core. Los estados que maneja:


Recepción:   RECIBIDA → ok | CLAVE ACCESO REGISTRADA → duplicado | EN PROCESAMIENTO → pendiente
Autorización: AUTORIZADO → valor | NO AUTORIZADO → valor (no error, para poder persistir)
              EN PROCESAMIENTO → retry hasta 5x con delay 2s
H4 — ServicioPdf.cs con iText 7
RIDE con iText 7 Community (itext7 NuGet, netstandard2.0 → .NET 4.8). Secciones:


(1) Encabezado 2 cols — logo + razón social / RUC, tipo doc, número, autorización, clave acceso
(2) Datos comprador
(3) Tabla ítems — descripción, cantidad, P.unitario, descuento, total s/imp, IVA
(4) Totales por tasa IVA
(5) Formas de pago
(6) Info adicional
H5-H6 — Facturacion.Api (WebAPI 2)
3 controllers:


FacturasController
  POST  /api/v1/facturas              → EmitirFactura
  POST  /api/v1/facturas/{id}/reintentar → ReintentarEmisionFactura
  GET   /api/v1/facturas/{id}/pdf     → signed URL o stream del PDF
  GET   /api/v1/facturas/{id}/xml     → stream del XML autorizado

EmpresasController
  PUT   /api/v1/empresas/{ruc}        → upsert empresa + certificado

ParametrosController
  GET   /api/v1/parametros/{ruc}
  PUT   /api/v1/parametros/{ruc}
CompositionRoot.cs — registro manual con Microsoft.Extensions.DependencyInjection + resolver para WebAPI 2:


public static IServiceProvider Configurar(HttpConfiguration config)
{
    var services = new ServiceCollection();

    // Repositorios
    services.AddScoped<IFacturasRepositorio, FacturasRepositorio>();
    services.AddScoped<IEmpresasRepositorio, EmpresasRepositorio>();
    services.AddScoped<ISecuencialesRepositorio, SecuencialesRepositorio>();
    services.AddScoped<IParametrosRepositorio, ParametrosRepositorio>();

    // Servicios infraestructura
    services.AddScoped<IServicioXml, ServicioXml>();
    services.AddScoped<IServicioFirma, ServicioFirma>();
    services.AddScoped<IServicioSri, ServicioSri>();
    services.AddScoped<IServicioPdf, ServicioPdf>();
    services.AddSingleton<IServicioStorage, ServicioStorageLocal>();

    // Casos de uso
    services.AddScoped<EmitirFactura>();
    services.AddScoped<ReintentarEmisionFactura>();

    // HttpClient para SRI
    services.AddHttpClient("sri");

    var provider = services.BuildServiceProvider();
    config.DependencyResolver = new MsDiDependencyResolver(provider);
    return provider;
}
Auth — copy de JwtHelper.cs de SF, adaptado:


// Global filter en WebApiConfig.cs
config.Filters.Add(new JwtAuthorizationFilter());
DÍA 3 — Primera factura real (6h)
H1 — Setup entorno dev
secrets.config con cadena de conexión SQL Server
.p12 de pruebas en carpeta local
Crear empresa + parametros + secuencial en BD
Configurar URL SRI: https://celcer.sri.gob.ec (pruebas)
H2-H4 — Primera factura end-to-end
Secuencia que debe funcionar al final del día:


POST /api/v1/facturas
  → IncrementarYObtenerAsync (secuencial = 1)
  → GeneradorClaveAcceso (49 dígitos)
  → Factura.Crear(...) → AgregarAsync (estado: PENDIENTE)
  → OrquestadorEmision:
      [1] ServicioXml.Generar → XML string
      [2] ServicioFirma.Firmar → XML firmado → guardar en disco
          → PersistirAsync (estado: PENDIENTE, xml_firmado_path set)
      [3] ServicioSri.EnviarRecepcion → RECIBIDA
          → PersistirAsync (estado: PENDIENTE_AUTORIZACION)
      [4] ServicioSri.ConsultarAutorizacion → AUTORIZADO
          → PersistirAsync (estado: AUTORIZADO_PENDIENTE_ARCHIVOS)
      [5] ServicioPdf.Generar → PDF → guardar en disco
          → PersistirAsync (estado: AUTORIZADO)
  → Response 201 { id, clave_acceso, estado_sri: "AUTORIZADO" }
H4-H6 — Debug
Lo que típicamente falla en el primer intento:

Encoding XML (BOM en UTF-8) → UTF8Encoding(false)
Orden de campos en <infoFactura> → seguir exactamente el XSD v1.1.0
Namespace XML incorrecto → verificar contra SF que ya funciona
.p12 con password incorrecto → verificar antes
Timeout SRI → reintentar, el ambiente de pruebas es lento a veces
DÍA 4 — Checkpoints + reintentos + edge cases (6h)
H1-H2 — Verificar los 5 checkpoints
Simular fallo en cada paso y verificar que el estado en BD queda correcto:

Fallo simulado	Estado esperado en BD	Qué tiene seteado
Antes de firmar	PENDIENTE	nada
Después de firmar, antes de SRI	PENDIENTE	xml_firmado_path
Después de envío SRI	PENDIENTE_AUTORIZACION	xml_firmado_path
Después de autorización	AUTORIZADO_PENDIENTE_ARCHIVOS	xml_autorizado_path, sin xml_firmado_path
Después de PDF	AUTORIZADO	todo seteado
H3-H4 — OrquestadorReintento
Para cada estado anterior, llamar POST /api/v1/facturas/{id}/reintentar y verificar que salta los pasos ya completados y termina en AUTORIZADO.

El caso más importante: CLAVE ACCESO REGISTRADA en reintento → el SRI ya tiene el documento, tratar como éxito y continuar a consultar autorización.

H5-H6 — Edge cases
NO_AUTORIZADO → XML firmado se borra del disco, estado persiste, secuencial queda libre
Secuencial duplicado en nueva emisión → ExisteSecuencialActivoAsync devuelve true → Errores.Secuencial.Duplicado → 409
Empresa no encontrada → Errores.Empresa.NoEncontrada → 404
.p12 password incorrecto → Errores.Firma.CertificadoInvalido → 422
DÍA 5 — Tests + hardening (6h)
H1-H3 — Tests unitarios

Facturacion.Tests/
├── GeneradorClaveAccesoTests.cs
│     ✓ clave tiene exactamente 49 dígitos
│     ✓ dígito verificador módulo 11 correcto
│     ✓ resultado 10 → convierte a 1
│     ✓ resultado 11 → convierte a 0
│
├── OrquestadorEmisionTests.cs
│     ✓ happy path → 5 checkpoints en orden → Autorizado
│     ✓ SRI NO_AUTORIZADO → borra XML firmado → NoAutorizado
│     ✓ fallo en PDF → estado AutorizadoPendienteArchivos persiste
│
├── OrquestadorReintentoTests.cs
│     ✓ reintento desde cada estado intermedio llega a Autorizado
│     ✓ CLAVE_ACCESO_REGISTRADA → continúa a consultar autorización
│
└── EmitirFacturaTests.cs
      ✓ secuencial se genera si no viene en request
      ✓ secuencial duplicado activo → 409
      ✓ empresa no encontrada → 404
H4-H5 — Hardening seguridad
CORS: solo https:// origins, headers explícitos
RequireHttpsAttribute global
Headers: X-Frame-Options, X-Content-Type-Options, HSTS
secrets.config en .gitignore, secrets.config.example como plantilla
Cifrado AES-256-GCM para cert_password en BD (copy de CertPasswordEncryption de FU)
Rate limiting básico por IP (MemoryCache, 60 req/min)
H6 — Deploy a servidor
Publicar Facturacion.Api en IIS del servidor .NET 4.8
Ejecutar schema.sql en SQL Server
Configurar secrets.config en el servidor
Smoke test: una factura real desde el servidor
Resumen de decisiones ya tomadas
Decisión	Valor
Ubicación	d:\Proyectos\FacturacionCore\
Base de datos	Nueva BD FacturacionCore en 92.204.64.145
PDF	iText 7 (netstandard2.0)
ORM	Dapper + Microsoft.Data.SqlClient
DI	Microsoft.Extensions.DependencyInjection + resolver WebAPI 2
Auth	JWT HMAC-SHA256 (copy de SF)
Firma	FirmadorNativo copiado de SF
Storage Día 1	Filesystem local — paths configurables
NC + Retenciones	Después del MVP — Día 6+
Lo que NO entra en esta semana
Notas de Crédito y Retenciones → misma estructura, ~1 día cada una después
Envío de correo → ProcesosGenerales.EnviarCorreoDocumento de SF se reutiliza
Preview PDF sin emitir → caso de uso GenerarPreviewPdf, ~medio día
Lote (ec:validarLote) → solo SF lo tiene, pendiente para después