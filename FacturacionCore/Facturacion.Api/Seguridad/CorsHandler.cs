using System;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Facturacion.Api.Seguridad
{
    // CORS manual (sin paquete extra). Permite solo los orígenes configurados en
    // appSettings "CorsOrigins" (separados por coma) y únicamente sobre HTTPS.
    public class CorsHandler : DelegatingHandler
    {
        private static readonly string[] OrigenesPermitidos =
            (ConfigurationManager.AppSettings["CorsOrigins"] ?? "")
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(o => o.Trim().TrimEnd('/'))
                .ToArray();

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var origin = request.Headers.Contains("Origin")
                ? request.Headers.GetValues("Origin").FirstOrDefault()
                : null;

            bool permitido = origin != null
                && origin.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                && OrigenesPermitidos.Contains(origin.TrimEnd('/'), StringComparer.OrdinalIgnoreCase);

            // Preflight: responder sin pasar por el pipeline.
            if (request.Method == HttpMethod.Options)
            {
                var preflight = new HttpResponseMessage(HttpStatusCode.OK);
                if (permitido) AgregarCabeceras(preflight, origin);
                return preflight;
            }

            var response = await base.SendAsync(request, cancellationToken);
            if (permitido) AgregarCabeceras(response, origin);
            return response;
        }

        private static void AgregarCabeceras(HttpResponseMessage resp, string origin)
        {
            resp.Headers.Add("Access-Control-Allow-Origin", origin);
            resp.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            resp.Headers.Add("Access-Control-Allow-Headers", "Authorization, Content-Type");
            resp.Headers.Add("Access-Control-Max-Age", "3600");
        }
    }
}
