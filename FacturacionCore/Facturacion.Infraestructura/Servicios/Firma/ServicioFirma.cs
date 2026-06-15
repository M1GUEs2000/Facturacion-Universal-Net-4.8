using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using Facturacion.Core;
using Facturacion.Core.Interfaces.Servicios;
using FirmaXadesNet;
using FirmaXadesNet.Crypto;
using FirmaXadesNet.Signature.Parameters;
using Microsoft.Extensions.Logging;

namespace Facturacion.Infraestructura.Servicios.Firma
{
    public class ServicioFirma : IServicioFirma
    {
        private readonly ILogger<ServicioFirma> _logger;
        private readonly SemaphoreSlim _semaforo;

        public ServicioFirma(ILogger<ServicioFirma> logger, SemaphoreSlim semaforo)
        {
            _logger   = logger;
            _semaforo = semaforo;
        }

        public async Task<ErrorOr<string>> FirmarXmlAsync(
            string xmlSinFirmar, byte[] certificadoP12, string certPassword)
        {
            await _semaforo.WaitAsync();
            try
            {
                return await Task.Run(() => Firmar(xmlSinFirmar, certificadoP12, certPassword));
            }
            finally
            {
                _semaforo.Release();
            }
        }

        private ErrorOr<string> Firmar(string xml, byte[] certificadoP12, string certPassword)
        {
            X509Certificate2 cert;
            try
            {
                cert = new X509Certificate2(
                    certificadoP12,
                    certPassword,
                    X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);
            }
            catch (CryptographicException ex)
            {
                _logger.LogError(ex, "Certificado P12 inválido o password incorrecto");
                return Errores.Firma.CertificadoInvalido;
            }

            try
            {
                var service   = new XadesService();
                var parametros = new SignatureParameters
                {
                    SignaturePackaging = SignaturePackaging.ENVELOPED,
                    InputMimeType      = "text/xml",
                    SignatureMethod    = SignatureMethod.RSAwithSHA1,
                    DigestMethod       = DigestMethod.SHA1
                };

                using (parametros.Signer = new Signer(cert))
                using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(xml)))
                {
                    var docFirmado = service.Sign(ms, parametros);
                    return docFirmado.Document.OuterXml;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error firmando XML con XAdES-BES");
                return Errores.Firma.ErrorFirma;
            }
            finally
            {
                cert.Dispose();
            }
        }
    }
}
