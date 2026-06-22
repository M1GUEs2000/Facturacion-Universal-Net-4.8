using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using Facturacion.Api.Jobs;
using Facturacion.Api.Logging;

namespace Facturacion.Api
{
    public class WebApiApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            // Serilog primero: el resto del arranque (CompositionRoot) ya loguea contra él.
            LoggingConfig.Configurar();

            AreaRegistration.RegisterAllAreas();
            GlobalConfiguration.Configure(WebApiConfig.Register);
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);

            // Hangfire al final: ya hay log y la app está configurada. Arranca el
            // BackgroundJobServer y registra el reintento automático recurrente.
            HangfireConfig.Configurar();
        }

        protected void Application_End()
        {
            // Shutdown ordenado del servidor de jobs antes de cerrar el log.
            HangfireConfig.Cerrar();
            // Vacía los buffers pendientes del sink de archivo al reciclar el AppPool.
            LoggingConfig.Cerrar();
        }
    }
}
