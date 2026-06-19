using System;
using Facturacion.Core.Interfaces.Servicios;
using Microsoft.Extensions.Logging;

namespace Facturacion.Infraestructura.Servicios.Seguridad
{
    // Escribe un evento de auditoría estructurado a través del logger (Serilog en el Api).
    // Cada propiedad va como campo nombrado para poder filtrar/consultar el log.
    public class AuditLogger : IAuditLogger
    {
        private readonly ILogger<AuditLogger> _logger;

        public AuditLogger(ILogger<AuditLogger> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void Registrar(RegistroAuditoria r)
        {
            if (r == null) return;

            _logger.LogInformation(
                "[AUDIT] {Tipo} Cliente={Cliente} Ruc={Ruc} Ip={Ip} Exito={Exito} CodigoError={CodigoError} Detalle={Detalle}",
                r.Tipo, r.Cliente ?? "-", r.Ruc ?? "-", r.Ip ?? "-", r.Exito, r.CodigoError ?? "-", r.Detalle ?? "-");
        }
    }
}
