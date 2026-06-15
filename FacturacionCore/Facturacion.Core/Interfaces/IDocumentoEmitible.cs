using Facturacion.Core.Enums;

namespace Facturacion.Core.Interfaces
{
    public interface IDocumentoEmitible
    {
        EstadoSri EstadoSri { get; }
        string EmpresaRuc { get; }
        string ClaveAcceso { get; }
        string XmlFirmadoPath { get; }
        string XmlAutorizadoPath { get; }
        string PdfPath { get; }
        string NumeroAutorizacion { get; }

        void RegistrarXmlFirmado(string path);
        void RegistrarEnvioSri();
        void RegistrarNoAutorizacion(string sriRespuesta);
        void RegistrarNumeroAutorizacion(string numeroAutorizacion, System.DateTimeOffset fechaAutorizacion, string sriRespuesta);
        void RegistrarAutorizacionSri(string numeroAutorizacion, System.DateTimeOffset fechaAutorizacion, string xmlAutorizadoPath, string sriRespuesta);
        void RegistrarPdf(string pdfPath);
    }
}
