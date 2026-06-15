using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Facturacion.Infraestructura.Persistencia
{
    // Serialización de las colecciones embebidas (formas_pago, info_adicional)
    // hacia/desde columnas NVARCHAR(MAX). Forma camelCase: {codigo, total, plazo, unidadTiempo}.
    internal static class JsonColumnas
    {
        private static readonly JsonSerializerOptions Opciones = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public static string Serializar<T>(T valor) => JsonSerializer.Serialize(valor, Opciones);

        public static List<T> Deserializar<T>(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new List<T>();
            return JsonSerializer.Deserialize<List<T>>(json, Opciones) ?? new List<T>();
        }
    }
}
