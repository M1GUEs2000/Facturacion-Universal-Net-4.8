using System;
using System.Collections.Generic;
using System.Configuration;
using System.Security.Cryptography;
using System.Text;

namespace Facturacion.Api.Auth
{
    // Valida pares cliente/secret antes de emitir un JWT. Los pares se configuran en
    // secrets.config (key "AuthClients", formato "cliente:secret;cliente2:secret2").
    // Falla cerrada: si no hay clientes configurados, ninguna credencial es válida.
    public static class CredencialesCliente
    {
        // cliente -> hash SHA-256 del secret esperado (no guardamos el secret en claro en memoria).
        private static readonly Lazy<Dictionary<string, byte[]>> Clientes =
            new Lazy<Dictionary<string, byte[]>>(Cargar);

        public static bool Validar(string cliente, string secret)
        {
            if (string.IsNullOrWhiteSpace(cliente) || string.IsNullOrEmpty(secret))
                return false;

            byte[] esperado;
            if (!Clientes.Value.TryGetValue(cliente, out esperado))
            {
                // Comparación dummy para no revelar por tiempo si el cliente existe o no.
                IgualdadTiempoFijo(Hash(secret), new byte[32]);
                return false;
            }

            return IgualdadTiempoFijo(Hash(secret), esperado);
        }

        private static Dictionary<string, byte[]> Cargar()
        {
            var mapa = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            var raw = ConfigurationManager.AppSettings["AuthClients"];
            if (string.IsNullOrWhiteSpace(raw)) return mapa;

            foreach (var par in raw.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var idx = par.IndexOf(':');
                if (idx <= 0 || idx == par.Length - 1) continue;

                var cliente = par.Substring(0, idx).Trim();
                var secret = par.Substring(idx + 1).Trim();
                if (cliente.Length == 0 || secret.Length == 0) continue;

                mapa[cliente] = Hash(secret);
            }
            return mapa;
        }

        private static byte[] Hash(string valor)
        {
            using (var sha = SHA256.Create())
                return sha.ComputeHash(Encoding.UTF8.GetBytes(valor));
        }

        // Comparación de tiempo constante: no corta en la primera diferencia.
        private static bool IgualdadTiempoFijo(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }
    }
}
