using System.Collections.Generic;
using System.Threading.Tasks;
using ErrorOr;
using Facturacion.Core.Enums;
using Facturacion.Core.Interfaces.Repositorios;
using Facturacion.Core.Interfaces.Servicios;

namespace Facturacion.Core.CasosDeUso.Facturas
{
    public class ComandoEnviarCorreoFactura
    {
        public int FacturaId { get; set; }
        public List<string> Destinatarios { get; set; } = new List<string>();
    }

    public class EnviarCorreoFactura
    {
        private readonly IFacturasRepositorio _facturas;
        private readonly IServicioStorage _storage;
        private readonly IServicioCorreo _correo;

        public EnviarCorreoFactura(IFacturasRepositorio facturas, IServicioStorage storage, IServicioCorreo correo)
        {
            _facturas = facturas;
            _storage = storage;
            _correo = correo;
        }

        public async Task<ErrorOr<Success>> EjecutarAsync(ComandoEnviarCorreoFactura cmd)
        {
            var factura = await _facturas.ObtenerPorIdAsync(cmd.FacturaId);
            if (factura == null)
                return Errores.Factura.NoEncontrada;

            if (factura.EstadoSri != EstadoSri.Autorizado)
                return Errores.Factura.NoAutorizada;

            var pdfResult = await _storage.ObtenerAsync(factura.PdfPath);
            if (pdfResult.IsError) return pdfResult.FirstError;

            var xmlResult = await _storage.ObtenerAsync(factura.XmlAutorizadoPath);
            if (xmlResult.IsError) return xmlResult.FirstError;

            var secuencial = factura.Estab + "-" + factura.PtoEmi + "-" + factura.Secuencial;
            var asunto = $"Factura Electrónica {secuencial}";
            var cuerpo = ComponerCuerpoHtml(factura.RazonSocialComprador, secuencial,
                factura.ImporteTotal, factura.NumeroAutorizacion);

            var mensaje = new MensajeCorreo
            {
                Para = cmd.Destinatarios,
                Asunto = asunto,
                CuerpoHtml = cuerpo,
                Adjuntos = new System.Collections.Generic.List<AdjuntoCorreo>
                {
                    new AdjuntoCorreo
                    {
                        NombreArchivo = "factura_" + secuencial.Replace("-", "") + ".pdf",
                        Contenido = pdfResult.Value,
                        TipoContenido = "application/pdf"
                    },
                    new AdjuntoCorreo
                    {
                        NombreArchivo = "factura_" + secuencial.Replace("-", "") + ".xml",
                        Contenido = xmlResult.Value,
                        TipoContenido = "application/xml"
                    }
                }
            };

            return await _correo.EnviarAsync(mensaje);
        }

        private static string ComponerCuerpoHtml(string razonSocial, string secuencial,
            decimal importeTotal, string numeroAutorizacion)
        {
            return $@"<!DOCTYPE html>
<html>
<body style=""font-family:Arial,sans-serif;color:#333;"">
  <p>Estimado/a <strong>{razonSocial}</strong>,</p>
  <p>Adjunto encontrará su factura electrónica con los siguientes datos:</p>
  <table style=""border-collapse:collapse;"">
    <tr><td style=""padding:4px 12px 4px 0;""><strong>Número:</strong></td><td>{secuencial}</td></tr>
    <tr><td style=""padding:4px 12px 4px 0;""><strong>Autorización SRI:</strong></td><td>{numeroAutorizacion}</td></tr>
    <tr><td style=""padding:4px 12px 4px 0;""><strong>Importe total:</strong></td><td>${importeTotal:N2}</td></tr>
  </table>
  <p>Se adjuntan el RIDE (PDF) y el XML autorizado.</p>
</body>
</html>";
        }
    }
}
