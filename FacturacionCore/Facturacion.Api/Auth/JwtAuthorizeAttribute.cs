using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Security.Principal;
using System.Threading;
using System.Web.Http.Controllers;
using System.Web.Http.Filters;

namespace Facturacion.Api.Auth
{
    // Filtro de autorización JWT para WebAPI 2. Exige "Authorization: Bearer <token>".
    public class JwtAuthorizeAttribute : AuthorizationFilterAttribute
    {
        public override void OnAuthorization(HttpActionContext actionContext)
        {
            var auth = actionContext.Request.Headers.Authorization;
            if (auth == null || auth.Scheme != "Bearer" || string.IsNullOrWhiteSpace(auth.Parameter))
            {
                Rechazar(actionContext);
                return;
            }

            string cliente;
            if (!JwtHelper.Validar(auth.Parameter, out cliente))
            {
                Rechazar(actionContext);
                return;
            }

            var identidad = new ClaimsIdentity("Jwt");
            identidad.AddClaim(new Claim("cliente", cliente ?? ""));
            IPrincipal principal = new ClaimsPrincipal(identidad);

            Thread.CurrentPrincipal = principal;
            if (actionContext.RequestContext != null)
                actionContext.RequestContext.Principal = principal;
        }

        private static void Rechazar(HttpActionContext ctx)
        {
            ctx.Response = ctx.Request.CreateErrorResponse(
                HttpStatusCode.Unauthorized, "Token JWT ausente o inválido.");
        }
    }
}
