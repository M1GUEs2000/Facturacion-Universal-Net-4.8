using System.Threading.Tasks;
using Facturacion.Core.Entidades;
using Facturacion.Core.Enums;

namespace Facturacion.Core.Interfaces.Repositorios
{
    public interface ISecuencialesRepositorio
    {
        Task<SecuencialSri> ObtenerAsync(string empresaRuc, string estab, string ptoEmi, TipoDocumentoSri tipoDocumento);
        Task<int> IncrementarYObtenerAsync(string empresaRuc, string estab, string ptoEmi, TipoDocumentoSri tipoDocumento);
        Task AgregarAsync(SecuencialSri secuencial);
        Task ActualizarAsync(SecuencialSri secuencial);
    }
}
