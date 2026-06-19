using System.Collections.Generic;
using System.Threading.Tasks;
using ErrorOr;

namespace Facturacion.Core.Interfaces.Servicios
{
    // Adjunto de un correo (ej: el PDF del RIDE o el XML autorizado).
    public sealed class AdjuntoCorreo
    {
        public string NombreArchivo { get; set; }
        public byte[] Contenido { get; set; }
        public string TipoContenido { get; set; }   // ej: "application/pdf", "application/xml"
    }

    // Mensaje a enviar. Genérico a propósito: el armado del correo de una factura
    // (asunto, cuerpo, adjuntos PDF+XML) se compone en una capa superior.
    public sealed class MensajeCorreo
    {
        public List<string> Para { get; set; } = new List<string>();
        public string Asunto { get; set; }
        public string CuerpoHtml { get; set; }
        public List<AdjuntoCorreo> Adjuntos { get; set; } = new List<AdjuntoCorreo>();
    }

    // Puerto de envío de correo. La implementación vive en Infraestructura (SMTP).
    public interface IServicioCorreo
    {
        Task<ErrorOr<Success>> EnviarAsync(MensajeCorreo mensaje);
    }
}
