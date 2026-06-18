using System.Web.Http;
using Facturacion.Api.Auth;
using Facturacion.Api.Models;

namespace Facturacion.Api.Controllers
{
    // Genera tokens JWT para pruebas/clientes. Sin [JwtAuthorize] (es el punto de entrada).
    [RoutePrefix("api/v1/auth")]
    public class AuthController : ApiController
    {
        [HttpPost, Route("token")]
        public IHttpActionResult Token(TokenRequest req)
        {
            var cliente = req != null && !string.IsNullOrWhiteSpace(req.Cliente) ? req.Cliente : "default";
            var token = JwtHelper.Generar(cliente);
            return Ok(new { token, cliente });
        }
    }
}
