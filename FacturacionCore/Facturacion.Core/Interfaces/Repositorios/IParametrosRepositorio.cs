using System.Threading.Tasks;
using Facturacion.Core.Entidades;

namespace Facturacion.Core.Interfaces.Repositorios
{
    public interface IParametrosRepositorio
    {
        Task<ParametrosFacturacion> ObtenerPorRucAsync(string empresaRuc);
        Task AgregarAsync(ParametrosFacturacion parametros);
        Task ActualizarAsync(ParametrosFacturacion parametros);
    }
}
