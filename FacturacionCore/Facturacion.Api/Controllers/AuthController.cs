using System.Web;
using System.Web.Http;
using Facturacion.Api.Auth;
using Facturacion.Api.Composition;
using Facturacion.Api.Models;
using Facturacion.Core;
using Serilog;

namespace Facturacion.Api.Controllers
{
    // Emite tokens JWT. Sin [JwtAuthorize] (es el punto de entrada), pero exige
    // credenciales cliente/secret válidas (configuradas en secrets.config → "AuthClients").
    // Está bajo rate limiting (política "auth") para frenar fuerza bruta.
    [RoutePrefix("api/v1/auth")]
    public class AuthController : ApiController
    {
        [HttpPost, Route("token")]
        public IHttpActionResult Token(TokenRequest req)
        {
            var cliente = req?.Cliente;
            var secret = req?.Secret;

            if (!CredencialesCliente.Validar(cliente, secret))
            {
                var ip = HttpContext.Current?.Request?.UserHostAddress ?? "-";
                Log.ForContext<AuthController>()
                   .Warning("Intento de emisión de token rechazado. Cliente={Cliente} Ip={Ip}",
                            string.IsNullOrWhiteSpace(cliente) ? "-" : cliente, ip);

                // 401 genérico: no revela si el cliente existe o si el secret es incorrecto.
                return this.DesdeError(Errores.Auth.CredencialesInvalidas);
            }

            var token = JwtHelper.Generar(cliente);
            return Ok(new { token, cliente });
        }
    }
}
