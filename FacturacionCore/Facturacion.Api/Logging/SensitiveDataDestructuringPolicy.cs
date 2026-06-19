using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Serilog.Core;
using Serilog.Events;

namespace Facturacion.Api.Logging
{
    // IDestructuringPolicy de Serilog: cuando se loguea un objeto con {@obj}, reemplaza
    // las propiedades sensibles (password del certificado y bytes/base64 del .p12) por
    // "***REDACTED***" para que nunca aterricen en el archivo de log.
    // Replica la SensitiveDataDestructuringPolicy del origen .NET 8 ([[flujo-api]]).
    public class SensitiveDataDestructuringPolicy : IDestructuringPolicy
    {
        public const string Marcador = "***REDACTED***";

        private static readonly HashSet<string> PropiedadesSensibles =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "CertPassword",
                "CertificadoP12",
                "CertificadoP12Base64",
                "Password"
            };

        public bool TryDestructure(object value, ILogEventPropertyValueFactory propertyValueFactory,
            out LogEventPropertyValue result)
        {
            result = null;
            if (value == null) return false;

            var tipo = value.GetType();
            if (tipo.IsPrimitive || tipo.IsEnum || value is string) return false;

            var propiedades = tipo.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                  .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
                                  .ToArray();

            // Solo intervenimos si el objeto realmente tiene algún campo sensible;
            // el resto lo maneja la destructuración por defecto de Serilog.
            if (!propiedades.Any(p => PropiedadesSensibles.Contains(p.Name)))
                return false;

            var logProps = new List<LogEventProperty>(propiedades.Length);
            foreach (var p in propiedades)
            {
                LogEventPropertyValue valor;
                if (PropiedadesSensibles.Contains(p.Name))
                {
                    valor = new ScalarValue(Marcador);
                }
                else
                {
                    object bruto;
                    try { bruto = p.GetValue(value); }
                    catch { bruto = "***ERROR***"; }
                    valor = propertyValueFactory.CreatePropertyValue(bruto, destructureObjects: true);
                }
                logProps.Add(new LogEventProperty(p.Name, valor));
            }

            result = new StructureValue(logProps, tipo.Name);
            return true;
        }
    }
}
