using System;
using Facturacion.Core.Enums;

namespace Facturacion.Core.Entidades
{
    public class SecuencialSri
    {
        public int Id { get; private set; }
        public string EmpresaRuc { get; private set; }
        public string Estab { get; private set; }
        public string PtoEmi { get; private set; }
        public TipoDocumentoSri TipoDocumento { get; private set; }
        public int UltimoSecuencial { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime UpdatedAt { get; private set; }

        private SecuencialSri() { }

        public void AsignarId(int id) => Id = id;

        public static SecuencialSri Crear(string empresaRuc, string estab, string ptoEmi, TipoDocumentoSri tipoDocumento)
        {
            return new SecuencialSri
            {
                EmpresaRuc = empresaRuc,
                Estab = estab,
                PtoEmi = ptoEmi,
                TipoDocumento = tipoDocumento,
                UltimoSecuencial = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }
    }
}
