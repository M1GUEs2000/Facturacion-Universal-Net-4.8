using System;
using Facturacion.Core.Enums;

namespace Facturacion.Infraestructura.Persistencia
{
    // estado_sri se persiste como VARCHAR(30) legible (necesario para el predicado
    // del índice filtrado UX_secuencial_activo). Estas cadenas son el contrato con la BD.
    internal static class EstadoSriMapper
    {
        public static string ABaseDatos(EstadoSri estado)
        {
            switch (estado)
            {
                case EstadoSri.Pendiente:                   return "PENDIENTE";
                case EstadoSri.Enviado:                      return "ENVIADO";
                case EstadoSri.PendienteAutorizacion:        return "PENDIENTE_AUTORIZACION";
                case EstadoSri.AutorizadoPendienteArchivos:  return "AUTORIZADO_PENDIENTE_ARCHIVOS";
                case EstadoSri.Autorizado:                   return "AUTORIZADO";
                case EstadoSri.NoAutorizado:                 return "NO_AUTORIZADO";
                case EstadoSri.Anulado:                      return "ANULADO";
                default:
                    throw new ArgumentOutOfRangeException(nameof(estado), estado, "EstadoSri no soportado.");
            }
        }

        public static EstadoSri DesdeBaseDatos(string valor)
        {
            switch (valor)
            {
                case "PENDIENTE":                     return EstadoSri.Pendiente;
                case "ENVIADO":                        return EstadoSri.Enviado;
                case "PENDIENTE_AUTORIZACION":         return EstadoSri.PendienteAutorizacion;
                case "AUTORIZADO_PENDIENTE_ARCHIVOS":  return EstadoSri.AutorizadoPendienteArchivos;
                case "AUTORIZADO":                     return EstadoSri.Autorizado;
                case "NO_AUTORIZADO":                  return EstadoSri.NoAutorizado;
                case "ANULADO":                        return EstadoSri.Anulado;
                default:
                    throw new ArgumentException("estado_sri desconocido en BD: " + valor, nameof(valor));
            }
        }
    }
}
