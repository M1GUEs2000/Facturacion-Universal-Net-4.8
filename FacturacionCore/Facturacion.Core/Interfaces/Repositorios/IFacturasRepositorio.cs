using System.Threading.Tasks;
using Facturacion.Core.Entidades;
using Facturacion.Core.Enums;

namespace Facturacion.Core.Interfaces.Repositorios
{
    public interface IFacturasRepositorio
    {
        Task<Factura> ObtenerPorIdAsync(int id);
        Task<Factura> ObtenerPorClaveAccesoAsync(string claveAcceso);
        Task<bool> ExisteSecuencialActivoAsync(string empresaRuc, string estab, string ptoEmi, string secuencial, Ambiente ambiente);
        Task AgregarAsync(Factura factura);
        Task ActualizarAsync(Factura factura);
    }
}
