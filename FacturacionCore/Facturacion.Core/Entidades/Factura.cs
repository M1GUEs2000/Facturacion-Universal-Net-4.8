using System;
using System.Collections.Generic;
using Facturacion.Core.Enums;

namespace Facturacion.Core.Entidades
{
    public class Factura : DocumentoElectronico
    {
        public string RazonSocialComprador { get; private set; }
        public string IdentificacionComprador { get; private set; }
        public TipoIdentificacion TipoIdentificacionComprador { get; private set; }
        public string DireccionComprador { get; private set; }
        public string GuiaRemision { get; private set; }
        public decimal TotalSinImpuestos { get; private set; }
        public decimal TotalDescuento { get; private set; }
        public decimal TotalIva { get; private set; }
        public decimal TotalIce { get; private set; }
        public decimal ImporteTotal { get; private set; }
        public string Moneda { get; private set; }
        public List<FacturaDetalle> Detalles { get; private set; }
        public List<FormaPago> FormasPago { get; private set; }
        public List<InfoAdicional> InfoAdicional { get; private set; }

        private Factura() { }

        public static Factura Crear(
            string empresaRuc,
            string claveAcceso,
            Ambiente ambiente,
            string estab,
            string ptoEmi,
            string secuencial,
            DateTime fechaEmision,
            string razonSocialComprador,
            string identificacionComprador,
            TipoIdentificacion tipoIdentificacion,
            string direccionComprador,
            decimal totalSinImpuestos,
            decimal totalDescuento,
            decimal totalIva,
            decimal totalIce,
            decimal importeTotal,
            List<FacturaDetalle> detalles,
            List<FormaPago> formasPago,
            List<InfoAdicional> infoAdicional = null,
            string guiaRemision = null,
            string moneda = "DOLAR")
        {
            return new Factura
            {
                EmpresaRuc = empresaRuc,
                ClaveAcceso = claveAcceso,
                Ambiente = ambiente,
                Estab = estab,
                PtoEmi = ptoEmi,
                Secuencial = secuencial,
                FechaEmision = fechaEmision,
                EstadoSri = EstadoSri.Pendiente,
                RazonSocialComprador = razonSocialComprador,
                IdentificacionComprador = identificacionComprador,
                TipoIdentificacionComprador = tipoIdentificacion,
                DireccionComprador = direccionComprador,
                GuiaRemision = guiaRemision,
                TotalSinImpuestos = totalSinImpuestos,
                TotalDescuento = totalDescuento,
                TotalIva = totalIva,
                TotalIce = totalIce,
                ImporteTotal = importeTotal,
                Moneda = moneda,
                Detalles = detalles ?? new List<FacturaDetalle>(),
                FormasPago = formasPago ?? new List<FormaPago>(),
                InfoAdicional = infoAdicional ?? new List<InfoAdicional>(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }
    }
}
