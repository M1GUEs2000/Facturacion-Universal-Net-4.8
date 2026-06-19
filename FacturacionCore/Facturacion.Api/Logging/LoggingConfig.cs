using System;
using System.Configuration;
using System.IO;
using System.Text;
using System.Web.Hosting;
using Serilog;
using Serilog.Events;

namespace Facturacion.Api.Logging
{
    // Configuración central de Serilog. Se invoca una sola vez en Application_Start,
    // antes que cualquier otra cosa, para que CompositionRoot ya tenga el log listo.
    public static class LoggingConfig
    {
        public static void Configurar()
        {
            var ruta = ResolverRutaLog();
            var nivel = ResolverNivelMinimo();

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Is(nivel)
                .Enrich.FromLogContext()
                .Destructure.With(new SensitiveDataDestructuringPolicy())
                .WriteTo.File(
                    path: ruta,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 31,
                    shared: true,
                    encoding: new UTF8Encoding(false),
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            Log.Information("Serilog inicializado. Logs en {Ruta}", ruta);
        }

        public static void Cerrar()
        {
            Log.CloseAndFlush();
        }

        private static string ResolverRutaLog()
        {
            var configurada = ConfigurationManager.AppSettings["LogPath"];
            if (string.IsNullOrWhiteSpace(configurada))
                configurada = "~/App_Data/logs/facturacion-.log";

            if (configurada.StartsWith("~") && HostingEnvironment.IsHosted)
                return HostingEnvironment.MapPath(configurada);

            // Fuera de IIS (tests, consola): resolver relativo al directorio base.
            if (configurada.StartsWith("~"))
                return Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                    configurada.TrimStart('~', '/', '\\').Replace('/', Path.DirectorySeparatorChar));

            return configurada;
        }

        private static LogEventLevel ResolverNivelMinimo()
        {
            var texto = ConfigurationManager.AppSettings["LogMinimumLevel"];
            LogEventLevel nivel;
            return Enum.TryParse(texto, ignoreCase: true, result: out nivel)
                ? nivel
                : LogEventLevel.Information;
        }
    }
}
