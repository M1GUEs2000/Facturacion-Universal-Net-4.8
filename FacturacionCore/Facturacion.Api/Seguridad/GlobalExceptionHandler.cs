using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web;
using System.Web.Http.ExceptionHandling;
using System.Web.Http.Results;
using Serilog;

namespace Facturacion.Api.Seguridad
{
    // Convierte excepciones no controladas en una respuesta ProblemDetails (RFC 7807).
    // El detalle real solo se expone si la app corre en modo debug; en producción
    // se devuelve un mensaje genérico para no filtrar internals/stack.
    public class GlobalExceptionHandler : ExceptionHandler
    {
        public override void Handle(ExceptionHandlerContext context)
        {
            var correlationId = context.Request.GetCorrelationId().ToString();

            // Log estructurado con la causa raíz; la excepción completa va al sink de archivo.
            Log.ForContext<GlobalExceptionHandler>()
               .Error(context.Exception, "Excepción no controlada. CorrelationId: {CorrelationId}", correlationId);

            bool mostrarDetalle = HttpContext.Current != null && HttpContext.Current.IsDebuggingEnabled;

            var problema = new
            {
                type   = "about:blank",
                title  = "Error interno del servidor",
                status = (int)HttpStatusCode.InternalServerError,
                detail = mostrarDetalle
                    ? context.Exception.GetBaseException().Message
                    : "Ocurrió un error procesando la solicitud.",
                traceId = correlationId
            };

            var response = context.Request.CreateResponse(HttpStatusCode.InternalServerError, problema);
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/problem+json");
            response.Headers.Add("X-Correlation-Id", correlationId);

            context.Result = new ResponseMessageResult(response);
        }
    }
}
