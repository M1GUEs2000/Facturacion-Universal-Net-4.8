using System.Threading.Tasks;
using ErrorOr;
using Facturacion.Core.Enums;

namespace Facturacion.Core.Interfaces.Servicios
{
    public interface IServicioSri
    {
        Task<ErrorOr<RespuestaRecepcionSri>> EnviarDocumentoAsync(string xmlFirmadoBase64, Ambiente ambiente);
        Task<ErrorOr<RespuestaAutorizacionSri>> ConsultarAutorizacionAsync(string claveAcceso, Ambiente ambiente);
    }
}
