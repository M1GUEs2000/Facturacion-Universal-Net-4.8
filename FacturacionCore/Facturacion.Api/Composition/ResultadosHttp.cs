using System;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Web.Http.Results;
using ErrorOr;

namespace Facturacion.Api.Composition
{
    public static class ResultadosHttp
    {
        public static IHttpActionResult Responder<T>(this ApiController c, ErrorOr<T> resultado,
            HttpStatusCode exito = HttpStatusCode.OK, Func<T, object> proyeccion = null)
        {
            if (resultado.IsError)
                return c.DesdeError(resultado.FirstError);

            object cuerpo = proyeccion != null ? proyeccion(resultado.Value) : (object)resultado.Value;
            return new ResponseMessageResult(c.Request.CreateResponse(exito, cuerpo));
        }

        public static IHttpActionResult DesdeError(this ApiController c, Error error)
        {
            var status = MapearEstado(error.Type);
            var cuerpo = new { codigo = error.Code, mensaje = error.Description };
            return new ResponseMessageResult(c.Request.CreateResponse(status, cuerpo));
        }

        private static HttpStatusCode MapearEstado(ErrorType tipo)
        {
            switch (tipo)
            {
                case ErrorType.Conflict:     return HttpStatusCode.Conflict;            // 409
                case ErrorType.NotFound:     return HttpStatusCode.NotFound;            // 404
                case ErrorType.Validation:   return HttpStatusCode.BadRequest;          // 400
                case ErrorType.Unauthorized: return HttpStatusCode.Unauthorized;        // 401
                case ErrorType.Forbidden:    return HttpStatusCode.Forbidden;           // 403
                case ErrorType.Unexpected:   return HttpStatusCode.InternalServerError; // 500
                // Failure = fallo de procesamiento (SRI rechazó, error de firma/PDF/storage),
                // no es culpa del request → 422, no 400.
                case ErrorType.Failure:      return (HttpStatusCode)422;                // 422 Unprocessable Entity
                default:                     return HttpStatusCode.BadRequest;          // 400
            }
        }
    }
}
