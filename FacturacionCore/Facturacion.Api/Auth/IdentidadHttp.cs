using System.Security.Claims;
using System.Web;
using System.Web.Http;

namespace Facturacion.Api.Auth
{
    // Helpers para leer la identidad del request (cliente del JWT) y la IP de origen,
    // usados al construir los registros de auditoría en los controllers.
    public static class IdentidadHttp
    {
        public static string ClienteActual(this ApiController c)
        {
            var principal = c.User as ClaimsPrincipal;
            var claim = principal?.FindFirst("cliente");
            return string.IsNullOrEmpty(claim?.Value) ? null : claim.Value;
        }

        public static string IpActual(this ApiController c)
        {
            return HttpContext.Current?.Request?.UserHostAddress;
        }
    }
}
