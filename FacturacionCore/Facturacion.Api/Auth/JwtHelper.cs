using System;
using System.Configuration;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Facturacion.Api.Auth
{
    // Generación y validación de JWT HMAC-SHA256. Secreto en secrets.config (appSettings).
    public static class JwtHelper
    {
        private static string Secret => ConfigurationManager.AppSettings["JwtSecret"];

        private static int HorasExpiracion =>
            int.TryParse(ConfigurationManager.AppSettings["JwtHorasExpiracion"], out int h) ? h : 8;

        public static string Generar(string cliente)
        {
            var secret = Secret;
            if (string.IsNullOrWhiteSpace(secret))
                throw new InvalidOperationException("JwtSecret no está configurado en secrets.config.");

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                claims: new[] { new Claim("cliente", cliente ?? "") },
                expires: DateTime.UtcNow.AddHours(HorasExpiracion),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public static bool Validar(string tokenStr, out string cliente)
        {
            cliente = null;
            if (string.IsNullOrWhiteSpace(tokenStr)) return false;

            var secret = Secret;
            if (string.IsNullOrWhiteSpace(secret)) return false;

            try
            {
                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
                var handler = new JwtSecurityTokenHandler();

                SecurityToken validado;
                handler.ValidateToken(tokenStr, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = key,
                    // Solo HMAC-SHA256: bloquea tokens con alg "none" o algoritmos débiles (alg confusion).
                    ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    // Exige exp y lo valida; 30s de tolerancia para drift de reloj entre hosts.
                    RequireExpirationTime = true,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30)
                }, out validado);

                // Defensa adicional: el token validado debe ser HMAC-SHA256.
                var jwt = validado as JwtSecurityToken;
                if (jwt == null ||
                    !string.Equals(jwt.Header.Alg, SecurityAlgorithms.HmacSha256, StringComparison.Ordinal))
                    return false;

                cliente = jwt.Claims.FirstOrDefault(c => c.Type == "cliente")?.Value;

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
