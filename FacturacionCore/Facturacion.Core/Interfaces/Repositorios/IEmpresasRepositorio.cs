using System.Threading.Tasks;
using Facturacion.Core.Entidades;

namespace Facturacion.Core.Interfaces.Repositorios
{
    public interface IEmpresasRepositorio
    {
        Task<Empresa> ObtenerPorRucAsync(string ruc);
        Task<bool> ExistePorRucAsync(string ruc);
        Task AgregarAsync(Empresa empresa);
        Task ActualizarAsync(Empresa empresa);
    }
}
