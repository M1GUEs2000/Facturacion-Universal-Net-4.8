namespace Facturacion.Core.Metodos
{
    public static class RutasStorage
    {
        public static string XmlFirmadoFactura(string ruc, string claveAcceso)
            => $"XMLFIRMADOS/{ruc}/{claveAcceso}.xml";

        public static string XmlAutorizadoFactura(string ruc, string claveAcceso)
            => $"XMLAUTORIZADOS/{ruc}/{claveAcceso}.xml";

        public static string PdfFactura(string ruc, string claveAcceso)
            => $"PDF/{ruc}/{claveAcceso}.pdf";
    }
}
