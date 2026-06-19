using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Facturacion.Api.Auth;

namespace Facturacion.Api.Seguridad
{
    // Rate limiting básico FixedWindow para WebAPI 2 (.NET 4.8 no tiene AddRateLimiter).
    // Particiona por cliente del JWT (si el token es válido) o por IP de origen.
    // Política según la ruta:
    //   "emision"   → /api/v1/facturas  → 60 req/min
    //   "escritura" → /api/v1/empresas  → 20 req/min
    // Al superar el límite: 429 Too Many Requests + header Retry-After.
    public class RateLimitingHandler : DelegatingHandler
    {
        private const int Http429 = 429;

        private sealed class Politica
        {
            public string Nombre;
            public int Limite;
            public TimeSpan Ventana;
        }

        private sealed class Contador
        {
            public int Conteo;
            public DateTime InicioVentana;
        }

        private static readonly Politica Emision =
            new Politica { Nombre = "emision", Limite = 60, Ventana = TimeSpan.FromMinutes(1) };

        private static readonly Politica Escritura =
            new Politica { Nombre = "escritura", Limite = 20, Ventana = TimeSpan.FromMinutes(1) };

        // Almacén en memoria. Para un despliegue de una sola instancia es suficiente;
        // tras balanceador/varias instancias habría que mover esto a un store compartido.
        private static readonly ConcurrentDictionary<string, Contador> Contadores =
            new ConcurrentDictionary<string, Contador>();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Los preflight CORS (OPTIONS) no consumen cuota.
            if (request.Method == HttpMethod.Options)
                return base.SendAsync(request, cancellationToken);

            var politica = SeleccionarPolitica(request);
            if (politica == null)
                return base.SendAsync(request, cancellationToken);

            var clave = politica.Nombre + "|" + Particion(request);
            var ahora = DateTime.UtcNow;

            var contador = Contadores.GetOrAdd(clave, _ => new Contador { Conteo = 0, InicioVentana = ahora });

            int conteo;
            DateTime inicio;
            lock (contador)
            {
                if (ahora - contador.InicioVentana >= politica.Ventana)
                {
                    contador.Conteo = 0;
                    contador.InicioVentana = ahora;
                }
                contador.Conteo++;
                conteo = contador.Conteo;
                inicio = contador.InicioVentana;
            }

            if (conteo > politica.Limite)
            {
                var resetEn = (int)Math.Ceiling((inicio + politica.Ventana - ahora).TotalSeconds);
                if (resetEn < 1) resetEn = 1;

                var respuesta = request.CreateResponse(
                    (HttpStatusCode)Http429,
                    new { codigo = "RATE_LIMIT", mensaje = "Demasiadas solicitudes. Reintente más tarde." });
                respuesta.Headers.Add("Retry-After", resetEn.ToString());
                return Task.FromResult(respuesta);
            }

            return base.SendAsync(request, cancellationToken);
        }

        private static Politica SeleccionarPolitica(HttpRequestMessage request)
        {
            var path = request.RequestUri.AbsolutePath ?? string.Empty;
            if (path.IndexOf("/api/v1/facturas", StringComparison.OrdinalIgnoreCase) >= 0)
                return Emision;
            if (path.IndexOf("/api/v1/empresas", StringComparison.OrdinalIgnoreCase) >= 0)
                return Escritura;
            return null;
        }

        private static string Particion(HttpRequestMessage request)
        {
            var auth = request.Headers.Authorization;
            if (auth != null && auth.Scheme == "Bearer" && !string.IsNullOrWhiteSpace(auth.Parameter))
            {
                string cliente;
                if (JwtHelper.Validar(auth.Parameter, out cliente) && !string.IsNullOrEmpty(cliente))
                    return "cli:" + cliente;
            }
            return "ip:" + ObtenerIp();
        }

        private static string ObtenerIp()
        {
            var ip = HttpContext.Current?.Request?.UserHostAddress;
            return string.IsNullOrEmpty(ip) ? "desconocida" : ip;
        }
    }
}
