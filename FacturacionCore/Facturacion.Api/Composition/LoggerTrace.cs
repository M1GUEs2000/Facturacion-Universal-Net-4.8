using System;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Facturacion.Api.Composition
{
    // ILogger<T> mínimo que escribe a System.Diagnostics.Trace.
    // Provisional: P-013 (hardening) cablea Serilog real.
    public class LoggerTrace<T> : ILogger<T>
    {
        private sealed class AlcanceNulo : IDisposable { public void Dispose() { } }
        private static readonly AlcanceNulo Alcance = new AlcanceNulo();

        public IDisposable BeginScope<TState>(TState state) => Alcance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception exception, Func<TState, Exception, string> formatter)
        {
            if (formatter == null) return;
            var msg = formatter(state, exception);
            Trace.WriteLine("[" + logLevel + "] " + typeof(T).Name + ": " + msg);
            if (exception != null)
                Trace.WriteLine(exception.ToString());
        }
    }
}
