using System.Collections.Generic;
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

        // IDs de facturas en un estado incompleto cuya última actualización es más
        // antigua que `antiguedadMinutos` (el delay evita colisionar con emisiones en
        // curso). Lo consume el reintento automático (Hangfire).
        Task<IReadOnlyList<int>> ObtenerIdsReintentablesAsync(IEnumerable<string> estados, int antiguedadMinutos);
    }
}
