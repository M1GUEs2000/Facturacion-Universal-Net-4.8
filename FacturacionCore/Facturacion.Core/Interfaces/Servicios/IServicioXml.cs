using Facturacion.Core.Entidades;

namespace Facturacion.Core.Interfaces.Servicios
{
    public interface IServicioXml
    {
        string GenerarXmlFactura(Factura factura, Empresa empresa, ParametrosFacturacion parametros);
    }
}
