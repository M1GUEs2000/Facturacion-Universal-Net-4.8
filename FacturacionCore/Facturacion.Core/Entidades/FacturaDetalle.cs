using System;
using Facturacion.Core.Enums;
using Facturacion.Core.Metodos;

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
            // Cálculos con precisión de 6 decimales; la presentación (XML/PDF) formatea a 2.
            var precioTotalSinImpuesto = Math.Round((cantidad * precioUnitario) - descuento, 6, MidpointRounding.AwayFromZero);
            var ivaBase = precioTotalSinImpuesto + iceValor;
            var tarifa = TarifasIva.Porcentaje(codigoIva);
            var ivaValor = Math.Round(ivaBase * tarifa / 100m, 6, MidpointRounding.AwayFromZero);

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
                IvaValor = ivaValor,
                IceValor = iceValor
            };
        }

        // Rehidratación desde persistencia — usado por los repositorios.
        public static FacturaDetalle Reconstituir(
            int id,
            int facturaId,
            string codigoPrincipal,
            string codigoAuxiliar,
            string descripcion,
            decimal cantidad,
            decimal precioUnitario,
            decimal descuento,
            decimal precioTotalSinImpuesto,
            CodigoIva codigoIva,
            decimal ivaBase,
            decimal ivaValor,
            decimal iceValor)
        {
            return new FacturaDetalle
            {
                Id = id,
                FacturaId = facturaId,
                CodigoPrincipal = codigoPrincipal,
                CodigoAuxiliar = codigoAuxiliar,
                Descripcion = descripcion,
                Cantidad = cantidad,
                PrecioUnitario = precioUnitario,
                Descuento = descuento,
                PrecioTotalSinImpuesto = precioTotalSinImpuesto,
                CodigoIva = codigoIva,
                IvaBase = ivaBase,
                IvaValor = ivaValor,
                IceValor = iceValor
            };
        }
    }
}
