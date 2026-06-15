namespace Facturacion.Core.Entidades
{
    public class FormaPago
    {
        public string Codigo { get; set; }
        public decimal Total { get; set; }
        public int? Plazo { get; set; }
        public string UnidadTiempo { get; set; }
    }
}
