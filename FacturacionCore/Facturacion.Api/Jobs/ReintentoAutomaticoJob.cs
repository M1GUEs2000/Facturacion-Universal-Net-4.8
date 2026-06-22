using System;
using System.Threading.Tasks;
using Facturacion.Api.Composition;
using Facturacion.Api.Logging;
using Facturacion.Core.CasosDeUso.Facturas;
using Facturacion.Core.Interfaces.Repositorios;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace Facturacion.Api.Jobs
{
    // Job recurrente: reintenta las facturas que quedaron en un estado incompleto.
    // Hangfire lo instancia con su JobActivator por defecto (Activator.CreateInstance),
    // de ahí el ctor sin parámetros que arma las dependencias desde el CompositionRoot
    // (mismo punto de composición que el resto de la API). El ctor interno existe para
    // los tests.
    public class ReintentoAutomaticoJob
    {
        // Estados que SÍ se reintentan. NO_AUTORIZADO queda fuera a propósito: el SRI
        // rechazó el XML y requiere intervención humana, no un reintento ciego.
        private static readonly string[] EstadosReintentables =
        {
            "PENDIENTE",
            "PENDIENTE_AUTORIZACION",
            "AUTORIZADO_PENDIENTE_ARCHIVOS"
        };

        private readonly IFacturasRepositorio _facturas;
        private readonly Func<ReintentarEmisionFactura> _crearReintento;
        private readonly ILogger<ReintentoAutomaticoJob> _logger;
        private readonly int _delayMinutos;

        public ReintentoAutomaticoJob()
            : this(
                CompositionRoot.Facturas(),
                CompositionRoot.ReintentarEmisionFactura,
                new SerilogLoggerAdapter<ReintentoAutomaticoJob>(),
                HangfireConfig.DelayMinutos)
        {
        }

        internal ReintentoAutomaticoJob(
            IFacturasRepositorio facturas,
            Func<ReintentarEmisionFactura> crearReintento,
            ILogger<ReintentoAutomaticoJob> logger,
            int delayMinutos)
        {
            _facturas = facturas;
            _crearReintento = crearReintento;
            _logger = logger;
            _delayMinutos = delayMinutos;
        }

        // AutomaticRetry(0): no reintentamos el batch completo si algo truena — el cron
        // lo vuelve a disparar en el siguiente intervalo. DisableConcurrentExecution evita
        // que dos corridas se solapen si una tarda más que el intervalo del cron.
        [AutomaticRetry(Attempts = 0)]
        [DisableConcurrentExecution(timeoutInSeconds: 300)]
        public async Task EjecutarAsync()
        {
            var ids = await _facturas.ObtenerIdsReintentablesAsync(EstadosReintentables, _delayMinutos);
            if (ids.Count == 0)
            {
                _logger.LogDebug("Reintento automático: sin facturas pendientes.");
                return;
            }

            _logger.LogInformation("Reintento automático: {Cantidad} factura(s) candidata(s).", ids.Count);

            int exitos = 0, conIncidencia = 0;
            foreach (var id in ids)
            {
                try
                {
                    // Caso de uso fresco por factura (es barato y mantiene el patrón
                    // "casos de uso por request"); ya es idempotente desde el checkpoint.
                    var resultado = await _crearReintento()
                        .EjecutarAsync(new ComandoReintentarEmisionFactura { FacturaId = id });

                    if (resultado.IsError)
                    {
                        conIncidencia++;
                        _logger.LogWarning(
                            "Reintento factura {FacturaId} no completó: {Error}",
                            id, resultado.FirstError.Description);
                    }
                    else
                    {
                        exitos++;
                        _logger.LogInformation(
                            "Reintento factura {FacturaId} → estado {Estado}.",
                            id, resultado.Value.EstadoSri);
                    }
                }
                catch (Exception ex)
                {
                    // El batch no se detiene: una factura problemática no debe frenar al resto.
                    conIncidencia++;
                    _logger.LogError(ex, "Error reintentando factura {FacturaId}.", id);
                }
            }

            _logger.LogInformation(
                "Reintento automático terminado: {Exitos} ok, {ConIncidencia} con incidencia de {Total}.",
                exitos, conIncidencia, ids.Count);
        }
    }
}
