using System;
using System.Configuration;
using Hangfire;
using Hangfire.SqlServer;
using Serilog;

namespace Facturacion.Api.Jobs
{
    // Arranque y parada de Hangfire. Igual que LoggingConfig, se invoca una sola vez
    // desde Application_Start (después de Serilog, para que el log ya exista) y se libera
    // en Application_End. Sin dashboard ni OWIN: solo el BackgroundJobServer embebido en
    // el AppDomain de la API. El estado de los jobs se ve en los logs Serilog y en las
    // tablas del esquema HangFire de la BD.
    //
    // Hangfire enruta su logging interno a Serilog automáticamente (LibLog detecta el
    // Serilog.Log.Logger estático ya configurado), por eso no se registra LogProvider.
    public static class HangfireConfig
    {
        private const string IdJobReintento = "reintento-automatico-facturas";
        private const string CronPorDefecto = "*/5 * * * *";
        private const int DelayPorDefecto = 5;

        private static BackgroundJobServer _server;

        // Minutos de antigüedad mínima de updated_at para que una factura sea candidata
        // a reintento (evita colisión con emisiones en curso). Lo lee el job al construirse.
        public static int DelayMinutos { get; private set; } = DelayPorDefecto;

        public static void Configurar()
        {
            // Permite desactivar Hangfire por completo desde config (p.ej. una instancia
            // que no deba correr el servidor de jobs detrás de un balanceador).
            if (!LeerBool("HangfireEnabled", true))
            {
                Log.Information("Hangfire deshabilitado por configuración (HangfireEnabled=false).");
                return;
            }

            var conn = ConfigurationManager.AppSettings["ConnectionString"];
            if (string.IsNullOrWhiteSpace(conn))
                throw new InvalidOperationException(
                    "ConnectionString no está configurado en secrets.config: Hangfire no puede arrancar.");

            DelayMinutos = LeerInt("HangfireReintentoDelayMinutos", DelayPorDefecto);

            var cron = ConfigurationManager.AppSettings["HangfireReintentoCron"];
            if (string.IsNullOrWhiteSpace(cron)) cron = CronPorDefecto;

            // Hangfire crea su esquema (tablas HangFire.*) en la misma BD si no existe.
            GlobalConfiguration.Configuration
                .UseSqlServerStorage(conn, new SqlServerStorageOptions
                {
                    QueuePollInterval = TimeSpan.FromSeconds(15),
                    SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
                    PrepareSchemaIfNecessary = true
                });

            // Un solo worker: el reintento es un batch secuencial y es el único job.
            _server = new BackgroundJobServer(new BackgroundJobServerOptions
            {
                WorkerCount = 1
            });

            RecurringJob.AddOrUpdate<ReintentoAutomaticoJob>(
                IdJobReintento,
                job => job.EjecutarAsync(),
                cron);

            Log.Information(
                "Hangfire iniciado. Job '{Job}' con cron '{Cron}', delay {Delay} min.",
                IdJobReintento, cron, DelayMinutos);
        }

        public static void Cerrar()
        {
            // Dispose hace shutdown ordenado: deja terminar el job en curso antes de salir.
            if (_server != null)
            {
                _server.Dispose();
                _server = null;
            }
        }

        private static bool LeerBool(string clave, bool porDefecto)
        {
            bool valor;
            return bool.TryParse(ConfigurationManager.AppSettings[clave], out valor) ? valor : porDefecto;
        }

        private static int LeerInt(string clave, int porDefecto)
        {
            int valor;
            return int.TryParse(ConfigurationManager.AppSettings[clave], out valor) ? valor : porDefecto;
        }
    }
}
