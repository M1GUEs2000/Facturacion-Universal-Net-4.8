using System;

namespace Facturacion.Core.Interfaces.Servicios
{
    public class RespuestaRecepcionSri
    {
        public string ClaveAcceso { get; }
        public string Estado { get; }

        public RespuestaRecepcionSri(string claveAcceso, string estado)
        {
            ClaveAcceso = claveAcceso;
            Estado = estado;
        }
    }

    public class RespuestaAutorizacionSri
    {
        public bool Autorizado { get; }
        public string NumeroAutorizacion { get; }
        public DateTimeOffset FechaAutorizacion { get; }
        public string XmlAutorizado { get; }
        public string SriRespuesta { get; }

        public RespuestaAutorizacionSri(bool autorizado, string numeroAutorizacion, DateTimeOffset fechaAutorizacion, string xmlAutorizado, string sriRespuesta)
        {
            Autorizado = autorizado;
            NumeroAutorizacion = numeroAutorizacion;
            FechaAutorizacion = fechaAutorizacion;
            XmlAutorizado = xmlAutorizado;
            SriRespuesta = sriRespuesta;
        }
    }
}
