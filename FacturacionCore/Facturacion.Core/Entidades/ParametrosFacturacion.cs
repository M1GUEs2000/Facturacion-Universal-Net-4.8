using System;

namespace Facturacion.Core.Entidades
{
    public class ParametrosFacturacion
    {
        public int Id { get; private set; }
        public string EmpresaRuc { get; private set; }
        public string Estab { get; private set; }
        public string PtoEmi { get; private set; }
        public string DireccionEstablecimiento { get; private set; }
        public string ContribuyenteEspecial { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime UpdatedAt { get; private set; }

        private ParametrosFacturacion() { }

        public static ParametrosFacturacion Crear(
            string empresaRuc,
            string estab,
            string ptoEmi,
            string direccionEstablecimiento,
            string contribuyenteEspecial = null)
        {
            return new ParametrosFacturacion
            {
                EmpresaRuc = empresaRuc,
                Estab = estab,
                PtoEmi = ptoEmi,
                DireccionEstablecimiento = direccionEstablecimiento,
                ContribuyenteEspecial = contribuyenteEspecial,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }
    }
}
