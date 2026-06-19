using System;
using System.Configuration;
using System.Threading;
using System.Web.Hosting;
using Facturacion.Api.Logging;
using Facturacion.Core.CasosDeUso.Comun;
using Facturacion.Core.CasosDeUso.Facturas;
using Facturacion.Core.Interfaces.Repositorios;
using Facturacion.Core.Interfaces.Servicios;
using Facturacion.Infraestructura.Persistencia;
using Facturacion.Infraestructura.Servicios.Correo;
using Facturacion.Infraestructura.Servicios.Firma;
using Facturacion.Infraestructura.Servicios.Pdf;
using Facturacion.Infraestructura.Servicios.Seguridad;
using Facturacion.Infraestructura.Servicios.Sri;
using Facturacion.Infraestructura.Servicios.Storage;
using Facturacion.Infraestructura.Servicios.Xml;

namespace Facturacion.Api.Composition
{
    // Composition root manual (sin contenedor): singletons para servicios sin estado,
    // los casos de uso se construyen por request (son baratos).
    public static class CompositionRoot
    {
        private static readonly Lazy<IFabricaConexion> _fabrica = new Lazy<IFabricaConexion>(CrearFabrica);
        private static readonly Lazy<IServicioFirma> _firma = new Lazy<IServicioFirma>(CrearFirma);
        private static readonly Lazy<IServicioSri> _sri = new Lazy<IServicioSri>(() => new ServicioSri(new SerilogLoggerAdapter<ServicioSri>()));
        private static readonly Lazy<IServicioPdf> _pdf = new Lazy<IServicioPdf>(() => new ServicioPdf(new SerilogLoggerAdapter<ServicioPdf>()));
        private static readonly Lazy<IServicioXml> _xml = new Lazy<IServicioXml>(() => new ServicioXml(new SerilogLoggerAdapter<ServicioXml>()));
        private static readonly Lazy<IServicioStorage> _storage = new Lazy<IServicioStorage>(CrearStorage);
        private static readonly Lazy<IProtectorCredenciales> _protector = new Lazy<IProtectorCredenciales>(CrearProtector);
        private static readonly Lazy<IAuditLogger> _audit = new Lazy<IAuditLogger>(() => new AuditLogger(new SerilogLoggerAdapter<AuditLogger>()));
        private static readonly Lazy<IServicioCorreo> _correo = new Lazy<IServicioCorreo>(CrearCorreo);

        // ─── Configuración ───────────────────────────────────────────────────

        private static IFabricaConexion CrearFabrica()
        {
            var conn = ConfigurationManager.AppSettings["ConnectionString"];
            if (string.IsNullOrWhiteSpace(conn))
                throw new InvalidOperationException("ConnectionString no está configurado en secrets.config.");
            return new FabricaConexion(conn);
        }

        private static IServicioFirma CrearFirma() =>
            new ServicioFirma(new SerilogLoggerAdapter<ServicioFirma>(), new SemaphoreSlim(1, 1));

        private static IProtectorCredenciales CrearProtector()
        {
            var clave = ConfigurationManager.AppSettings["CertEncryptionKey"];
            return new ProtectorCredencialesAes(clave);
        }

        private static IServicioCorreo CrearCorreo()
        {
            int puerto;
            bool ssl;
            var config = new ConfiguracionSmtp
            {
                Host = ConfigurationManager.AppSettings["SmtpHost"],
                Puerto = int.TryParse(ConfigurationManager.AppSettings["SmtpPort"], out puerto) ? puerto : 587,
                // Default true: solo es false si está configurado explícitamente como "false".
                UsarSsl = !bool.TryParse(ConfigurationManager.AppSettings["SmtpSsl"], out ssl) || ssl,
                Usuario = ConfigurationManager.AppSettings["SmtpUsuario"],
                Password = ConfigurationManager.AppSettings["SmtpPassword"],
                Remitente = ConfigurationManager.AppSettings["SmtpRemitente"],
                NombreRemitente = ConfigurationManager.AppSettings["SmtpNombreRemitente"]
            };
            return new ServicioCorreoSmtp(config, new SerilogLoggerAdapter<ServicioCorreoSmtp>());
        }

        private static IServicioStorage CrearStorage()
        {
            var ruta = ConfigurationManager.AppSettings["StorageBasePath"];
            if (string.IsNullOrWhiteSpace(ruta))
                ruta = "~/App_Data/archivos";
            if (ruta.StartsWith("~") && HostingEnvironment.IsHosted)
                ruta = HostingEnvironment.MapPath(ruta);
            return new ServicioStorageLocal(ruta, new SerilogLoggerAdapter<ServicioStorageLocal>());
        }

        // ─── Repositorios ──────────────────────────────────────────────────────

        public static IEmpresasRepositorio Empresas() => new EmpresasRepositorio(_fabrica.Value, _protector.Value);
        public static IParametrosRepositorio Parametros() => new ParametrosRepositorio(_fabrica.Value);
        public static ISecuencialesRepositorio Secuenciales() => new SecuencialesRepositorio(_fabrica.Value);
        public static IFacturasRepositorio Facturas() => new FacturasRepositorio(_fabrica.Value);

        // ─── Servicios transversales ────────────────────────────────────────────

        public static IAuditLogger Auditoria() => _audit.Value;
        public static IFabricaConexion Conexion() => _fabrica.Value;
        public static IServicioCorreo Correo() => _correo.Value;

        // ─── Casos de uso ──────────────────────────────────────────────────────

        public static EmitirFactura EmitirFactura()
        {
            var orquestador = new OrquestadorEmision(_firma.Value, _sri.Value, _pdf.Value, _storage.Value);
            return new EmitirFactura(Facturas(), Empresas(), Parametros(), Secuenciales(), _xml.Value, orquestador);
        }

        public static ReintentarEmisionFactura ReintentarEmisionFactura()
        {
            var orquestador = new OrquestadorReintento(_firma.Value, _sri.Value, _pdf.Value, _storage.Value);
            return new ReintentarEmisionFactura(Facturas(), Empresas(), Parametros(), _xml.Value, orquestador);
        }
    }
}
