using System;
using System.Text;
using System.Threading.Tasks;
using ErrorOr;
using Facturacion.Core.Entidades;
using Facturacion.Core.Interfaces;
using Facturacion.Core.Interfaces.Servicios;
using Facturacion.Core.Metodos;

namespace Facturacion.Core.CasosDeUso.Comun
{
    public class ParametrosEmision<TDoc> where TDoc : DocumentoElectronico, IDocumentoEmitible
    {
        public TDoc Documento { get; set; }
        public string XmlSinFirmar { get; set; }
        public byte[] CertificadoP12 { get; set; }
        public string CertPassword { get; set; }
        public string StoragePrefijo { get; set; }
        public Func<TDoc, Task> Persistir { get; set; }
        public Empresa Empresa { get; set; }
        public ParametrosFacturacion ParametrosFacturacion { get; set; }
    }

    public class OrquestadorEmision
    {
        private readonly IServicioFirma _firma;
        private readonly IServicioSri _sri;
        private readonly IServicioPdf _pdf;
        private readonly IServicioStorage _storage;

        public OrquestadorEmision(
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

        public async Task<ErrorOr<TDoc>> EjecutarAsync<TDoc>(ParametrosEmision<TDoc> p)
            where TDoc : DocumentoElectronico, IDocumentoEmitible
        {
            // CP1: Firma + storage XML firmado
            var firmaResult = await _firma.FirmarXmlAsync(p.XmlSinFirmar, p.CertificadoP12, p.CertPassword);
            if (firmaResult.IsError) return firmaResult.FirstError;

            var xmlFirmado = firmaResult.Value;
            var rutaFirmado = RutasStorage.XmlFirmadoFactura(p.Documento.EmpresaRuc, p.Documento.ClaveAcceso);

            var guardadoFirmado = await _storage.GuardarAsync(rutaFirmado, Encoding.UTF8.GetBytes(xmlFirmado));
            if (guardadoFirmado.IsError) return guardadoFirmado.FirstError;

            p.Documento.RegistrarXmlFirmado(rutaFirmado);

            // CP2: Recepción SRI
            var xmlBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(xmlFirmado));
            var recepcionResult = await _sri.EnviarDocumentoAsync(xmlBase64, p.Documento.Ambiente);

            if (recepcionResult.IsError)
            {
                if (recepcionResult.FirstError.Code == Errores.Sri.CodigoSecuencialDuplicado)
                {
                    // Ya enviado antes — continuar a autorización
                }
                else
                {
                    return recepcionResult.FirstError;
                }
            }

            p.Documento.RegistrarEnvioSri();
            await p.Persistir(p.Documento);

            // CP3: Consultar autorización
            var autorizacionResult = await _sri.ConsultarAutorizacionAsync(p.Documento.ClaveAcceso, p.Documento.Ambiente);
            if (autorizacionResult.IsError) return autorizacionResult.FirstError;

            var autorizacion = autorizacionResult.Value;

            if (!autorizacion.Autorizado)
            {
                await _storage.EliminarAsync(rutaFirmado);
                p.Documento.RegistrarNoAutorizacion(autorizacion.SriRespuesta);
                await p.Persistir(p.Documento);
                return Errores.Sri.NoAutorizado(autorizacion.SriRespuesta);
            }

            p.Documento.RegistrarNumeroAutorizacion(
                autorizacion.NumeroAutorizacion,
                autorizacion.FechaAutorizacion,
                autorizacion.SriRespuesta);

            // CP4: Guardar XML autorizado + borrar XML firmado
            var rutaAutorizado = RutasStorage.XmlAutorizadoFactura(p.Documento.EmpresaRuc, p.Documento.ClaveAcceso);
            var guardadoAutorizado = await _storage.GuardarAsync(rutaAutorizado, Encoding.UTF8.GetBytes(autorizacion.XmlAutorizado));
            if (guardadoAutorizado.IsError) return guardadoAutorizado.FirstError;

            await _storage.EliminarAsync(rutaFirmado);

            p.Documento.RegistrarAutorizacionSri(
                autorizacion.NumeroAutorizacion,
                autorizacion.FechaAutorizacion,
                rutaAutorizado,
                autorizacion.SriRespuesta);
            await p.Persistir(p.Documento);

            // CP5: Generar PDF
            var pdfResult = await GenerarPdfAsync(p);
            if (pdfResult.IsError) return pdfResult.FirstError;

            var rutaPdf = RutasStorage.PdfFactura(p.Documento.EmpresaRuc, p.Documento.ClaveAcceso);
            var guardadoPdf = await _storage.GuardarAsync(rutaPdf, pdfResult.Value);
            if (guardadoPdf.IsError) return guardadoPdf.FirstError;

            p.Documento.RegistrarPdf(rutaPdf);
            await p.Persistir(p.Documento);

            return p.Documento;
        }

        private async Task<ErrorOr<byte[]>> GenerarPdfAsync<TDoc>(ParametrosEmision<TDoc> p)
            where TDoc : DocumentoElectronico, IDocumentoEmitible
        {
            if (p.Documento is Factura factura)
                return await _pdf.GenerarRideFacturaAsync(factura, p.Empresa, p.ParametrosFacturacion);

            return Errores.Pdf.ErrorGeneracion;
        }
    }
}
