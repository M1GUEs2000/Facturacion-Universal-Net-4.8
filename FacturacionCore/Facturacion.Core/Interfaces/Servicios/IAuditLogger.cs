namespace Facturacion.Core.Interfaces.Servicios
{
    // Eventos auditables del sistema. Se registran tras el resultado del caso de uso,
    // tanto en éxito como en error, para dejar rastro de quién hizo qué.
    public enum EventoAuditoria
    {
        EmpresaCreada,
        EmpresaActualizada,
        CertificadoActualizado,
        ParametrosCreados,
        FacturaEmitida,
        FacturaReintentada,
        CorreoEnviado
    }

    // Registro de auditoría. Nunca debe contener datos sensibles (passwords, bytes del .p12).
    public sealed class RegistroAuditoria
    {
        public EventoAuditoria Tipo { get; set; }
        public string Cliente { get; set; }      // claim "cliente" del JWT
        public string Ruc { get; set; }
        public string Ip { get; set; }
        public bool Exito { get; set; }
        public string CodigoError { get; set; }   // null si Exito = true
        public string Detalle { get; set; }       // opcional: id de factura, clave de acceso, etc.
    }

    // Rastro de auditoría. La implementación escribe un evento estructurado (Serilog).
    public interface IAuditLogger
    {
        void Registrar(RegistroAuditoria registro);
    }
}
