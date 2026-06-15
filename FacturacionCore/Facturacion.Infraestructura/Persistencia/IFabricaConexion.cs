using System.Data;

namespace Facturacion.Infraestructura.Persistencia
{
    public interface IFabricaConexion
    {
        IDbConnection Crear();
    }
}
