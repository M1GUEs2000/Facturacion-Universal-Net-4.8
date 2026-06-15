using System;
using System.Text;
using System.Threading.Tasks;
using ErrorOr;
using Facturacion.Core.Entidades;
using Facturacion.Core.Enums;
using Facturacion.Core.Interfaces;
using Facturacion.Core.Interfaces.Servicios;
using Facturacion.Core.Metodos;

namespace Facturacion.Core.CasosDeUso.Comun
{
    public class ParametrosReintento<TDoc> where TDoc : DocumentoElectronico, IDocumentoEmitible
    {
        public TDoc Documento { get; set; }
        public byte[] CertificadoP12 { get; set; }
        public string CertPassword { get; set; }
        public Func<string> GenerarXmlSinFirmar { get; set; }
        public Func<TDoc, Task> Persistir { get; set; }
        public Empresa Empresa { get; set; }
        public ParametrosFacturacion ParametrosFacturacion { get; set; }
    }

    public class OrquestadorReintento
    {
        private readonly IServicioFirma _firma;
        private readonly IServicioSri _sri;
        private readonly IServicioPdf _pdf;
        private readonly IServicioStorage _storage;

        public OrquestadorReintento(
            IServicioFirma firma,
            IServicioSri sri,
            IServicioPdf pdf,
            IServicioStorage storage)
        {
            _firma = firma;
            _sri = sri;
            _pdf = pdf;
            _storage = storage;
        }

        public async Task<ErrorOr<TDoc>> EjecutarAsync<TDoc>(ParametrosReintento<TDoc> p)
            where TDoc : DocumentoElectronico, IDocumentoEmitible
        {
            var doc = p.Documento;
            string xmlFirmado = null;

            // Re-firmar si no hay XML firmado en storage
            if (doc.XmlFirmadoPath == null)
            {
                var xmlSinFirmar = p.GenerarXmlSinFirmar();
                var firmaResult = await _firma.FirmarXmlAsync(xmlSinFirmar, p.CertificadoP12, p.CertPassword);
                if (firmaResult.IsError) return firmaResult.FirstError;

                xmlFirmado = firmaResult.Value;
                var rutaFirmado = RutasStorage.XmlFirmadoFactura(doc.EmpresaRuc, doc.ClaveAcceso);
                var guardado = await _storage.GuardarAsync(rutaFirmado, Encoding.UTF8.GetBytes(xmlFirmado));
                if (guardado.IsError) return guardado.FirstError;

                doc.RegistrarXmlFirmado(rutaFirmado);
            }

            // Re-enviar al SRI si aún no fue recibido
            if (doc.EstadoSri < EstadoSri.PendienteAutorizacion)
            {
                if (xmlFirmado == null)
                {
                    var obtenerResult = await _storage.ObtenerAsync(doc.XmlFirmadoPath);
                    if (obtenerResult.IsError) return obtenerResult.FirstError;
                    xmlFirmado = Encoding.UTF8.GetString(obtenerResult.Value);
                }

                var xmlBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(xmlFirmado));
                var recepcionResult = await _sri.EnviarDocumentoAsync(xmlBase64, doc.Ambiente);

                if (recepcionResult.IsError && recepcionResult.FirstError.Code != "Sri.SecuencialDuplicado")
                    return recepcionResult.FirstError;

                doc.RegistrarEnvioSri();
                await p.Persistir(doc);
            }

            // Re-consultar autorización si aún no hay XML autorizado
            if (doc.XmlAutorizadoPath == null)
            {
                var autorizacionResult = await _sri.ConsultarAutorizacionAsync(doc.ClaveAcceso, doc.Ambiente);
                if (autorizacionResult.IsError) return autorizacionResult.FirstError;

                var autorizacion = autorizacionResult.Value;

                if (!autorizacion.Autorizado)
                {
                    if (doc.XmlFirmadoPath != null)
                        await _storage.EliminarAsync(doc.XmlFirmadoPath);

                    doc.RegistrarNoAutorizacion(autorizacion.SriRespuesta);
                    await p.Persistir(doc);
                    return Errores.Sri.NoAutorizado(autorizacion.SriRespuesta);
                }

                var rutaAutorizado = RutasStorage.XmlAutorizadoFactura(doc.EmpresaRuc, doc.ClaveAcceso);
                var guardadoAut = await _storage.GuardarAsync(rutaAutorizado, Encoding.UTF8.GetBytes(autorizacion.XmlAutorizado));
                if (guardadoAut.IsError) return guardadoAut.FirstError;

                if (doc.XmlFirmadoPath != null)
                    await _storage.EliminarAsync(doc.XmlFirmadoPath);

                doc.RegistrarAutorizacionSri(
                    autorizacion.NumeroAutorizacion,
                    autorizacion.FechaAutorizacion,
                    rutaAutorizado,
                    autorizacion.SriRespuesta);
                await p.Persistir(doc);
            }

            // Generar PDF si falta
            if (doc.PdfPath == null)
            {
                var pdfResult = await GenerarPdfAsync(p);
                if (pdfResult.IsError) return pdfResult.FirstError;

                var rutaPdf = RutasStorage.PdfFactura(doc.EmpresaRuc, doc.ClaveAcceso);
                var guardadoPdf = await _storage.GuardarAsync(rutaPdf, pdfResult.Value);
                if (guardadoPdf.IsError) return guardadoPdf.FirstError;

                doc.RegistrarPdf(rutaPdf);
                await p.Persistir(doc);
            }

            return doc;
        }

        private async Task<ErrorOr<byte[]>> GenerarPdfAsync<TDoc>(ParametrosReintento<TDoc> p)
            where TDoc : DocumentoElectronico, IDocumentoEmitible
        {
            if (p.Documento is Factura factura)
                return await _pdf.GenerarRideFacturaAsync(factura, p.Empresa, p.ParametrosFacturacion);

            return Errores.Pdf.ErrorGeneracion;
        }
    }
}
