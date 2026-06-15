using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using ErrorOr;
using Facturacion.Core;
using Facturacion.Core.Enums;
using Facturacion.Core.Interfaces.Servicios;
using Microsoft.Extensions.Logging;

namespace Facturacion.Infraestructura.Servicios.Sri
{
    public class ServicioSri : IServicioSri
    {
        private const string RecepcionPruebas =
            "https://celcer.sri.gob.ec/comprobantes-electronicos-ws/RecepcionComprobantesOffline";
        private const string RecepcionProduccion =
            "https://cel.sri.gob.ec/comprobantes-electronicos-ws/RecepcionComprobantesOffline";
        private const string AutorizacionPruebas =
            "https://celcer.sri.gob.ec/comprobantes-electronicos-ws/AutorizacionComprobantesOffline";
        private const string AutorizacionProduccion =
            "https://cel.sri.gob.ec/comprobantes-electronicos-ws/AutorizacionComprobantesOffline";

        private const int MaxIntentosAutorizacion = 5;
        private static readonly TimeSpan DelayEntreIntentos = TimeSpan.FromSeconds(2);

        // HttpClient estático (reutilizado para evitar agotamiento de sockets).
        private static readonly HttpClient Http;

        static ServicioSri()
        {
            // El SRI exige TLS 1.2; en .NET 4.8 no siempre está activo por defecto.
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            Http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        }

        private readonly ILogger<ServicioSri> _logger;

        public ServicioSri(ILogger<ServicioSri> logger)
        {
            _logger = logger;
        }

        // ─── Recepción ────────────────────────────────────────────────────────────

        public async Task<ErrorOr<RespuestaRecepcionSri>> EnviarDocumentoAsync(string xmlFirmadoBase64, Ambiente ambiente)
        {
            var endpoint = ambiente == Ambiente.Pruebas ? RecepcionPruebas : RecepcionProduccion;
            var soap = BuildSoapRecepcion(xmlFirmadoBase64);

            string respuesta;
            try
            {
                respuesta = await PostSoapAsync(endpoint, soap);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Error HTTP enviando documento al SRI ({Endpoint})", endpoint);
                return Errores.Sri.ErrorComunicacion;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado enviando documento al SRI");
                return Errores.Sri.ErrorComunicacion;
            }

            return ParsearRecepcion(respuesta);
        }

        private ErrorOr<RespuestaRecepcionSri> ParsearRecepcion(string respuesta)
        {
            XDocument doc;
            try { doc = XDocument.Parse(respuesta); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Respuesta de recepción SRI no es XML válido. Snippet: {Snippet}", Snippet(respuesta));
                return Errores.Sri.ErrorComunicacion;
            }

            var nodo = PrimerElemento(doc, "RespuestaRecepcionComprobante");
            if (nodo == null)
                return Errores.Sri.SinRespuesta;

            var estado = ValorHijo(nodo, "estado") ?? "";
            var mensajes = ExtraerMensajes(doc);

            if (estado == "RECIBIDA")
                return new RespuestaRecepcionSri(null, estado);

            // Clave de acceso ya registrada → tratar como duplicado para que el
            // orquestador continúe directo a la consulta de autorización.
            if (EsClaveRegistrada(mensajes))
                return Errores.Sri.SecuencialDuplicado;

            if (estado == "EN PROCESAMIENTO")
                return Errores.Sri.EnProcesamiento;

            return Errores.Sri.Devuelta(FormatearMensajes(mensajes));
        }

        // ─── Autorización ──────────────────────────────────────────────────────────

        public async Task<ErrorOr<RespuestaAutorizacionSri>> ConsultarAutorizacionAsync(string claveAcceso, Ambiente ambiente)
        {
            var endpoint = ambiente == Ambiente.Pruebas ? AutorizacionPruebas : AutorizacionProduccion;
            var soap = BuildSoapAutorizacion(claveAcceso);

            ErrorOr<RespuestaAutorizacionSri> ultima = Errores.Sri.SinRespuesta;

            for (int intento = 1; intento <= MaxIntentosAutorizacion; intento++)
            {
                string respuesta;
                try
                {
                    respuesta = await PostSoapAsync(endpoint, soap);
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogError(ex, "Error HTTP consultando autorización SRI ({Endpoint})", endpoint);
                    return Errores.Sri.ErrorComunicacion;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error inesperado consultando autorización SRI");
                    return Errores.Sri.ErrorComunicacion;
                }

                ultima = ParsearAutorizacion(respuesta);

                // Solo se reintenta mientras el SRI siga EN PROCESAMIENTO.
                if (ultima.IsError
                    && ultima.FirstError.Code == "Sri.EnProcesamiento"
                    && intento < MaxIntentosAutorizacion)
                {
                    await Task.Delay(DelayEntreIntentos);
                    continue;
                }

                return ultima;
            }

            return ultima;
        }

        private ErrorOr<RespuestaAutorizacionSri> ParsearAutorizacion(string respuesta)
        {
            XDocument doc;
            try { doc = XDocument.Parse(respuesta); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Respuesta de autorización SRI no es XML válido. Snippet: {Snippet}", Snippet(respuesta));
                return Errores.Sri.ErrorComunicacion;
            }

            var root = PrimerElemento(doc, "RespuestaAutorizacionComprobante");
            if (root == null)
                return Errores.Sri.SinRespuesta;

            var auth = PrimerElemento(root, "autorizacion");
            if (auth == null)
                return Errores.Sri.EnProcesamiento; // todavía sin nodo de autorización

            var estado = ValorHijo(auth, "estado") ?? "";
            var mensajes = ExtraerMensajes(root);
            var resumen = FormatearMensajes(mensajes);

            if (estado == "AUTORIZADO")
            {
                var numero = ValorHijo(auth, "numeroAutorizacion");
                var fechaStr = ValorHijo(auth, "fechaAutorizacion");
                var xmlAutorizado = ValorHijo(auth, "comprobante");

                DateTimeOffset fecha;
                if (string.IsNullOrEmpty(fechaStr) || !DateTimeOffset.TryParse(fechaStr, out fecha))
                    fecha = DateTimeOffset.Now;

                return new RespuestaAutorizacionSri(true, numero, fecha, xmlAutorizado, resumen);
            }

            // NO AUTORIZADO se devuelve como VALOR (Autorizado=false) para que el caso de
            // uso persista el documento como NoAutorizado antes de retornar el error.
            if (estado == "NO AUTORIZADO")
                return new RespuestaAutorizacionSri(false, null, default(DateTimeOffset), null, resumen);

            if (estado == "EN PROCESAMIENTO")
                return Errores.Sri.EnProcesamiento;

            return Errores.Sri.SinRespuesta;
        }

        // ─── SOAP builders ──────────────────────────────────────────────────────────

        private static string BuildSoapRecepcion(string base64Xml)
        {
            return
                "<soapenv:Envelope xmlns:soapenv=\"http://schemas.xmlsoap.org/soap/envelope/\" xmlns:ec=\"http://ec.gob.sri.ws.recepcion\">" +
                "<soapenv:Header/>" +
                "<soapenv:Body>" +
                "<ec:validarComprobante>" +
                "<xml>" + base64Xml + "</xml>" +
                "</ec:validarComprobante>" +
                "</soapenv:Body>" +
                "</soapenv:Envelope>";
        }

        private static string BuildSoapAutorizacion(string claveAcceso)
        {
            return
                "<soapenv:Envelope xmlns:soapenv=\"http://schemas.xmlsoap.org/soap/envelope/\" xmlns:ec=\"http://ec.gob.sri.ws.autorizacion\">" +
                "<soapenv:Header/>" +
                "<soapenv:Body>" +
                "<ec:autorizacionComprobante>" +
                "<claveAccesoComprobante>" + System.Security.SecurityElement.Escape(claveAcceso) + "</claveAccesoComprobante>" +
                "</ec:autorizacionComprobante>" +
                "</soapenv:Body>" +
                "</soapenv:Envelope>";
        }

        // ─── HTTP ─────────────────────────────────────────────────────────────────

        private static async Task<string> PostSoapAsync(string endpoint, string soap)
        {
            using (var content = new StringContent(soap, Encoding.UTF8, "text/xml"))
            using (var response = await Http.PostAsync(endpoint, content))
            {
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
        }

        // ─── Helpers ──────────────────────────────────────────────────────────────

        private static XElement PrimerElemento(XContainer contenedor, string nombreLocal)
        {
            return contenedor.Descendants().FirstOrDefault(e => e.Name.LocalName == nombreLocal);
        }

        private static string ValorHijo(XElement elemento, string nombreLocal)
        {
            var hijo = elemento.Descendants().FirstOrDefault(e => e.Name.LocalName == nombreLocal);
            return hijo?.Value;
        }

        private static List<MensajeSri> ExtraerMensajes(XContainer contenedor)
        {
            return contenedor.Descendants()
                .Where(e => e.Name.LocalName == "mensaje"
                         && (e.Elements().Any(x => x.Name.LocalName == "identificador")
                          || e.Elements().Any(x => x.Name.LocalName == "informacionAdicional")
                          || e.Elements().Any(x => x.Name.LocalName == "tipo")))
                .Select(e => new MensajeSri(
                    ValorHijo(e, "identificador") ?? "",
                    ValorHijo(e, "mensaje") ?? "",
                    ValorHijo(e, "informacionAdicional") ?? ""))
                .ToList();
        }

        private static bool EsClaveRegistrada(List<MensajeSri> mensajes)
        {
            return mensajes.Any(m =>
                m.Identificador == "43"
                || (m.Mensaje != null && m.Mensaje.ToUpperInvariant().Contains("CLAVE ACCESO REGISTRADA")));
        }

        private static string FormatearMensajes(List<MensajeSri> mensajes)
        {
            if (mensajes == null || mensajes.Count == 0) return null;

            return string.Join(" | ", mensajes.Select(m =>
                string.IsNullOrEmpty(m.InformacionAdicional)
                    ? m.Mensaje
                    : m.Mensaje + ": " + m.InformacionAdicional));
        }

        private static string Snippet(string texto)
        {
            if (string.IsNullOrEmpty(texto)) return "";
            return texto.Length <= 500 ? texto : texto.Substring(0, 500);
        }

        private class MensajeSri
        {
            public string Identificador { get; }
            public string Mensaje { get; }
            public string InformacionAdicional { get; }

            public MensajeSri(string identificador, string mensaje, string informacionAdicional)
            {
                Identificador = identificador;
                Mensaje = mensaje;
                InformacionAdicional = informacionAdicional;
            }
        }
    }
}
