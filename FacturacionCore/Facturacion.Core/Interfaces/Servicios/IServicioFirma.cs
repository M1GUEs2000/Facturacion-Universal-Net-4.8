using System.Threading.Tasks;
using ErrorOr;

namespace Facturacion.Core.Interfaces.Servicios
{
    public interface IServicioFirma
    {
        Task<ErrorOr<string>> FirmarXmlAsync(string xmlSinFirmar, byte[] certificadoP12, string certPassword);
    }
}
