using System.Web.Http;
using System.Web.Http.Dispatcher;
using Facturacion.Api.Composition;
using FluentValidation.WebApi;
using Newtonsoft.Json.Serialization;

namespace Facturacion.Api
{
    public static class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {
            // Excepciones no controladas → ProblemDetails (RFC 7807) sin filtrar stack.
            config.Services.Replace(typeof(System.Web.Http.ExceptionHandling.IExceptionHandler), new Seguridad.GlobalExceptionHandler());

            // CORS: solo orígenes HTTPS configurados en appSettings "CorsOrigins".
            config.MessageHandlers.Add(new Seguridad.CorsHandler());

            // Rate limiting FixedWindow (emisión 60/min, escritura 20/min) por cliente/IP.
            config.MessageHandlers.Add(new Seguridad.RateLimitingHandler());

            // Composition root manual: resuelve los controllers de la API con sus dependencias.
            config.Services.Replace(typeof(IHttpControllerActivator), new ResolvedorControladores());

            // Rutas por atributo + ruta convencional de respaldo.
            config.MapHttpAttributeRoutes();
            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}/{id}",
                defaults: new { id = RouteParameter.Optional });

            // JSON camelCase como único formato (sin XML).
            var json = config.Formatters.JsonFormatter;
            json.SerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
            config.Formatters.Remove(config.Formatters.XmlFormatter);

            // Validación con FluentValidation (descubre validadores vía [Validator] en los modelos).
            FluentValidationModelValidatorProvider.Configure(config);
        }
    }
}
