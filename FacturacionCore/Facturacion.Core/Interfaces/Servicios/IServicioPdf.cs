using System.Threading.Tasks;
using ErrorOr;
using Facturacion.Core.Entidades;

namespace Facturacion.Core.Interfaces.Servicios
{
    public interface IServicioPdf
    {
        Task<ErrorOr<byte[]>> GenerarRideFacturaAsync(Factura factura, Empresa empresa, ParametrosFacturacion parametros);
    }
}
