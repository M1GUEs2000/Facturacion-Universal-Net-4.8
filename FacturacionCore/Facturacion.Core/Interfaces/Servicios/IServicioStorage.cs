using System.Threading.Tasks;
using ErrorOr;

namespace Facturacion.Core.Interfaces.Servicios
{
    public interface IServicioStorage
    {
        Task<ErrorOr<string>> GuardarAsync(string ruta, byte[] contenido);
        Task<ErrorOr<byte[]>> ObtenerAsync(string ruta);
        Task<ErrorOr<bool>> EliminarAsync(string ruta);
    }
}
