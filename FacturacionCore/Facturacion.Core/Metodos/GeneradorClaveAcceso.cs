using System;
using Facturacion.Core.Enums;

namespace Facturacion.Core.Metodos
{
    public static class GeneradorClaveAcceso
    {
        public static string Generar(
            DateTime fechaEmision,
            TipoDocumentoSri tipoDocumento,
            string ruc,
            Ambiente ambiente,
            string estab,
            string ptoEmi,
            string secuencial,
            string codigoNumerico)
        {
            var fecha = fechaEmision.ToString("ddMMyyyy");
            var tipo = ((int)tipoDocumento).ToString("D2");
            var amb = ((int)ambiente).ToString();

            var base48 = $"{fecha}{tipo}{ruc}{amb}{estab}{ptoEmi}{secuencial}{codigoNumerico}1";

            if (base48.Length != 48)
                throw new InvalidOperationException($"La clave base debe tener 48 dígitos, tiene {base48.Length}.");

            var digitoVerificador = CalcularModulo11(base48);
            return base48 + digitoVerificador;
        }

        private static int CalcularModulo11(string clave)
        {
            int[] pesos = { 2, 3, 4, 5, 6, 7 };
            int suma = 0;

            for (int i = clave.Length - 1; i >= 0; i--)
            {
                int digito = int.Parse(clave[i].ToString());
                int peso = pesos[(clave.Length - 1 - i) % 6];
                suma += digito * peso;
            }

            int residuo = suma % 11;
            int verificador = 11 - residuo;

            if (verificador == 11) return 0;
            if (verificador == 10) return 1;
            return verificador;
        }
    }
}
