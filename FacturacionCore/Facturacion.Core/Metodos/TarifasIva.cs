using Facturacion.Core.Enums;

namespace Facturacion.Core.Metodos
{
    // Fuente única de la tarifa (%) por código de IVA del catálogo SRI.
    public static class TarifasIva
    {
        public static decimal Porcentaje(CodigoIva codigo)
        {
            switch (codigo)
            {
                case CodigoIva.Iva12:            return 12m;
                case CodigoIva.Iva14:            return 14m;
                case CodigoIva.Iva15:            return 15m;
                case CodigoIva.Iva5:             return 5m;
                case CodigoIva.Iva8Diferenciado: return 8m;
                case CodigoIva.Iva13:            return 13m;
                default:                         return 0m; // IvaCero, NoObjeto, Exento
            }
        }
    }
}
