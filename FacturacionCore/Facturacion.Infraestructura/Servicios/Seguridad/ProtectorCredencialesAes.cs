using System;
using System.Security.Cryptography;
using System.Text;
using Facturacion.Core.Interfaces.Servicios;

namespace Facturacion.Infraestructura.Servicios.Seguridad
{
    // Cifrado autenticado AES-256-CBC + HMAC-SHA256 (encrypt-then-MAC).
    // Reemplaza a AES-GCM (no disponible en .NET Framework 4.8) para datos en reposo.
    // Formato del valor cifrado (base64): IV(16) || ciphertext || tag(32).
    // La clave maestra (>=32 bytes, base64) viene de configuración (secrets.config: CertEncryptionKey).
    public class ProtectorCredencialesAes : IProtectorCredenciales
    {
        private const int TamanoIv = 16;
        private const int TamanoTag = 32; // HMACSHA256

        private readonly byte[] _claveCifrado;
        private readonly byte[] _claveMac;

        public ProtectorCredencialesAes(string claveMaestraBase64)
        {
            if (string.IsNullOrWhiteSpace(claveMaestraBase64))
                throw new InvalidOperationException("CertEncryptionKey no está configurada en secrets.config.");

            byte[] maestra;
            try { maestra = Convert.FromBase64String(claveMaestraBase64); }
            catch (FormatException) { throw new InvalidOperationException("CertEncryptionKey no es base64 válido."); }

            if (maestra.Length < 32)
                throw new InvalidOperationException("CertEncryptionKey debe tener al menos 32 bytes.");

            // Subclaves separadas para cifrado y MAC (no reutilizar la misma clave en ambos roles).
            _claveCifrado = DerivarSubclave(maestra, "encryption");
            _claveMac     = DerivarSubclave(maestra, "authentication");
        }

        public string Cifrar(string textoPlano)
        {
            if (textoPlano == null) textoPlano = string.Empty;

            using (var aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.Mode    = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key     = _claveCifrado;
                aes.GenerateIV();
                var iv = aes.IV;

                byte[] cifrado;
                using (var enc = aes.CreateEncryptor())
                {
                    var plano = Encoding.UTF8.GetBytes(textoPlano);
                    cifrado = enc.TransformFinalBlock(plano, 0, plano.Length);
                }

                byte[] tag;
                using (var hmac = new HMACSHA256(_claveMac))
                    tag = hmac.ComputeHash(Concat(iv, cifrado));

                return Convert.ToBase64String(Concat(iv, cifrado, tag));
            }
        }

        public string Descifrar(string textoCifrado)
        {
            if (string.IsNullOrEmpty(textoCifrado))
                throw new CryptographicException("Valor cifrado vacío.");

            byte[] todo;
            try { todo = Convert.FromBase64String(textoCifrado); }
            catch (FormatException) { throw new CryptographicException("Valor cifrado no es base64 válido."); }

            if (todo.Length < TamanoIv + TamanoTag)
                throw new CryptographicException("Valor cifrado con formato inválido.");

            var iv = new byte[TamanoIv];
            Buffer.BlockCopy(todo, 0, iv, 0, TamanoIv);

            int largoCifrado = todo.Length - TamanoIv - TamanoTag;
            var cifrado = new byte[largoCifrado];
            Buffer.BlockCopy(todo, TamanoIv, cifrado, 0, largoCifrado);

            var tag = new byte[TamanoTag];
            Buffer.BlockCopy(todo, TamanoIv + largoCifrado, tag, 0, TamanoTag);

            // Verificar MAC antes de descifrar (encrypt-then-MAC).
            using (var hmac = new HMACSHA256(_claveMac))
            {
                var tagEsperado = hmac.ComputeHash(Concat(iv, cifrado));
                if (!ComparacionConstante(tag, tagEsperado))
                    throw new CryptographicException("Falló la verificación de integridad (HMAC). Clave incorrecta o dato alterado.");
            }

            using (var aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.Mode    = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key     = _claveCifrado;
                aes.IV      = iv;
                using (var dec = aes.CreateDecryptor())
                {
                    var plano = dec.TransformFinalBlock(cifrado, 0, cifrado.Length);
                    return Encoding.UTF8.GetString(plano);
                }
            }
        }

        private static byte[] DerivarSubclave(byte[] maestra, string etiqueta)
        {
            using (var hmac = new HMACSHA256(maestra))
                return hmac.ComputeHash(Encoding.UTF8.GetBytes(etiqueta));
        }

        private static byte[] Concat(params byte[][] partes)
        {
            int total = 0;
            foreach (var p in partes) total += p.Length;
            var r = new byte[total];
            int off = 0;
            foreach (var p in partes) { Buffer.BlockCopy(p, 0, r, off, p.Length); off += p.Length; }
            return r;
        }

        private static bool ComparacionConstante(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            int dif = 0;
            for (int i = 0; i < a.Length; i++) dif |= a[i] ^ b[i];
            return dif == 0;
        }
    }
}
