using System.Collections.Generic;
using Facturacion.Core.Enums;

namespace Facturacion.Core.Entidades
{
    public class FacturaDetalle
    {
        public int Id { get; private set; }
        public int FacturaId { get; private set; }
        public string CodigoPrincipal { get; private set; }
        public string CodigoAuxiliar { get; private set; }
        public string Descripcion { get; private set; }
        public decimal Cantidad { get; private set; }
        public decimal PrecioUnitario { get; private set; }
        public decimal Descuento { get; private set; }
        public decimal PrecioTotalSinImpuesto { get; private set; }
        public CodigoIva CodigoIva { get; private set; }
        public decimal IvaBase { get; private set; }
        public decimal IvaValor { get; private set; }
        public decimal IceValor { get; private set; }

        private FacturaDetalle() { }

        public static FacturaDetalle Crear(
            string codigoPrincipal,
            string descripcion,
            decimal cantidad,
            decimal precioUnitario,
            decimal descuento,
            CodigoIva codigoIva,
            decimal iceValor = 0,
            string codigoAuxiliar = null)
        {
            var precioTotalSinImpuesto = (cantidad * precioUnitario) - descuento;
            var ivaBase = precioTotalSinImpuesto + iceValor;

            return new FacturaDetalle
            {
                CodigoPrincipal = codigoPrincipal,
                CodigoAuxiliar = codigoAuxiliar,
                Descripcion = descripcion,
                Cantidad = cantidad,
                PrecioUnitario = precioUnitario,
                Descuento = descuento,
                PrecioTotalSinImpuesto = precioTotalSinImpuesto,
                CodigoIva = codigoIva,
                IvaBase = ivaBase,
                IvaValor = 0,
                IceValor = iceValor
            };
        }
    }
}
