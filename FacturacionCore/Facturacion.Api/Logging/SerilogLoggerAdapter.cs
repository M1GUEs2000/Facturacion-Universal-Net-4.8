using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Serilog.Events;
using ISerilogLogger = Serilog.ILogger;

namespace Facturacion.Api.Logging
{
    // Adaptador ILogger<T> (Microsoft.Extensions.Logging) -> Serilog.
    // Evita sumar el paquete Serilog.Extensions.Logging: traduce el LogLevel y
    // re-emite el message template + args originales a Serilog para conservar el
    // logging estructurado (y que la SensitiveDataDestructuringPolicy se aplique a {@obj}).
    public class SerilogLoggerAdapter<T> : ILogger<T>
    {
        private static readonly ISerilogLogger Logger = Serilog.Log.ForContext(typeof(T));

        private sealed class AlcanceNulo : IDisposable { public void Dispose() { } }
        private static readonly AlcanceNulo Alcance = new AlcanceNulo();

        public IDisposable BeginScope<TState>(TState state) => Alcance;

        public bool IsEnabled(LogLevel logLevel) =>
            logLevel != LogLevel.None && Logger.IsEnabled(MapearNivel(logLevel));

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception exception, Func<TState, Exception, string> formatter)
        {
            if (logLevel == LogLevel.None) return;
            var nivel = MapearNivel(logLevel);
            if (!Logger.IsEnabled(nivel)) return;

            // El state de MEL para `Log("template {X}", args)` es un
            // IReadOnlyList<KeyValuePair<string,object>> con "{OriginalFormat}" + los args.
            // Lo desempaquetamos para re-emitir el template estructurado a Serilog.
            string template = null;
            if (state is IEnumerable<KeyValuePair<string, object>> propiedades)
            {
                var args = new List<object>();
                foreach (var kv in propiedades)
                {
                    if (kv.Key == "{OriginalFormat}")
                        template = kv.Value as string;
                    else
                        args.Add(kv.Value);
                }

                if (template != null)
                {
                    Logger.Write(nivel, exception, template, args.ToArray());
                    return;
                }
            }

            // Fallback: sin template estructurado, usamos el mensaje ya formateado.
            var mensaje = formatter != null
                ? formatter(state, exception)
                : (state == null ? string.Empty : state.ToString());
            Logger.Write(nivel, exception, "{Mensaje}", mensaje);
        }

        private static LogEventLevel MapearNivel(LogLevel logLevel)
        {
            switch (logLevel)
            {
                case LogLevel.Trace: return LogEventLevel.Verbose;
                case LogLevel.Debug: return LogEventLevel.Debug;
                case LogLevel.Information: return LogEventLevel.Information;
                case LogLevel.Warning: return LogEventLevel.Warning;
                case LogLevel.Error: return LogEventLevel.Error;
                case LogLevel.Critical: return LogEventLevel.Fatal;
                default: return LogEventLevel.Information;
            }
        }
    }
}
