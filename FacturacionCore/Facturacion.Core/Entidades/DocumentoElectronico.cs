using System;
using Facturacion.Core.Enums;
using Facturacion.Core.Interfaces;

namespace Facturacion.Core.Entidades
{
    public abstract class DocumentoElectronico : IDocumentoEmitible
    {
        public int Id { get; protected set; }
        public string EmpresaRuc { get; protected set; }
        public string ClaveAcceso { get; protected set; }
        public Ambiente Ambiente { get; protected set; }
        public string Estab { get; protected set; }
        public string PtoEmi { get; protected set; }
        public string Secuencial { get; protected set; }
        public DateTime FechaEmision { get; protected set; }
        public EstadoSri EstadoSri { get; protected set; }
        public string XmlFirmadoPath { get; protected set; }
        public string XmlAutorizadoPath { get; protected set; }
        public string PdfPath { get; protected set; }
        public string NumeroAutorizacion { get; protected set; }
        public DateTimeOffset? FechaAutorizacion { get; protected set; }
        public string SriRespuesta { get; protected set; }
        public DateTime CreatedAt { get; protected set; }
        public DateTime UpdatedAt { get; protected set; }

        protected DocumentoElectronico() { }

        // Asigna el Id generado por la BD (IDENTITY) tras el INSERT.
        public void AsignarId(int id) => Id = id;

        public void RegistrarXmlFirmado(string path)
        {
            XmlFirmadoPath = path;
            EstadoSri = EstadoSri.Pendiente;
            UpdatedAt = DateTime.UtcNow;
        }

        public void RegistrarEnvioSri()
        {
            EstadoSri = EstadoSri.PendienteAutorizacion;
            UpdatedAt = DateTime.UtcNow;
        }

        public void RegistrarNoAutorizacion(string sriRespuesta)
        {
            XmlFirmadoPath = null;
            EstadoSri = EstadoSri.NoAutorizado;
            SriRespuesta = sriRespuesta;
            UpdatedAt = DateTime.UtcNow;
        }

        public void RegistrarNumeroAutorizacion(string numeroAutorizacion, DateTimeOffset fechaAutorizacion, string sriRespuesta)
        {
            NumeroAutorizacion = numeroAutorizacion;
            FechaAutorizacion = fechaAutorizacion;
            SriRespuesta = sriRespuesta;
            UpdatedAt = DateTime.UtcNow;
        }

        public void RegistrarAutorizacionSri(string numeroAutorizacion, DateTimeOffset fechaAutorizacion, string xmlAutorizadoPath, string sriRespuesta)
        {
            NumeroAutorizacion = numeroAutorizacion;
            FechaAutorizacion = fechaAutorizacion;
            XmlAutorizadoPath = xmlAutorizadoPath;
            XmlFirmadoPath = null;
            SriRespuesta = sriRespuesta;
            EstadoSri = EstadoSri.AutorizadoPendienteArchivos;
            UpdatedAt = DateTime.UtcNow;
        }

        public void RegistrarPdf(string pdfPath)
        {
            PdfPath = pdfPath;
            EstadoSri = EstadoSri.Autorizado;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
