using System.Threading.Tasks;
using Dapper;
using Facturacion.Core.Entidades;
using Facturacion.Core.Interfaces.Repositorios;
using Facturacion.Core.Interfaces.Servicios;

namespace Facturacion.Infraestructura.Persistencia
{
    public class EmpresasRepositorio : IEmpresasRepositorio
    {
        private readonly IFabricaConexion _fabrica;
        private readonly IProtectorCredenciales _protector;

        public EmpresasRepositorio(IFabricaConexion fabrica, IProtectorCredenciales protector)
        {
            _fabrica = fabrica;
            _protector = protector;
        }

        public async Task<Empresa> ObtenerPorRucAsync(string ruc)
        {
            const string sql = @"
                SELECT id, ruc, razon_social, nombre_comercial, dir_matriz,
                       obligado_contabilidad, certificado_path, cert_password,
                       created_at, updated_at
                FROM dbo.empresas
                WHERE ruc = @ruc;";

            using (var con = _fabrica.Crear())
            {
                var empresa = await con.QueryFirstOrDefaultAsync<Empresa>(sql, new { ruc });
                // cert_password se guarda cifrado en BD → descifrar al rehidratar.
                if (empresa != null)
                    empresa.AsignarCertPassword(_protector.Descifrar(empresa.CertPassword));
                return empresa;
            }
        }

        public async Task<bool> ExistePorRucAsync(string ruc)
        {
            const string sql = @"
                SELECT CASE WHEN EXISTS (SELECT 1 FROM dbo.empresas WHERE ruc = @ruc)
                            THEN 1 ELSE 0 END;";

            using (var con = _fabrica.Crear())
                return await con.ExecuteScalarAsync<bool>(sql, new { ruc });
        }

        public async Task AgregarAsync(Empresa empresa)
        {
            const string sql = @"
                INSERT INTO dbo.empresas
                    (ruc, razon_social, nombre_comercial, dir_matriz,
                     obligado_contabilidad, certificado_path, cert_password,
                     created_at, updated_at)
                OUTPUT INSERTED.id
                VALUES
                    (@Ruc, @RazonSocial, @NombreComercial, @DirMatriz,
                     @ObligadoContabilidad, @CertificadoPath, @CertPassword,
                     @CreatedAt, @UpdatedAt);";

            using (var con = _fabrica.Crear())
            {
                var id = await con.ExecuteScalarAsync<int>(sql, new
                {
                    empresa.Ruc,
                    empresa.RazonSocial,
                    empresa.NombreComercial,
                    empresa.DirMatriz,
                    empresa.ObligadoContabilidad,
                    empresa.CertificadoPath,
                    CertPassword = _protector.Cifrar(empresa.CertPassword),
                    empresa.CreatedAt,
                    empresa.UpdatedAt
                });
                empresa.AsignarId(id);
            }
        }

        public async Task ActualizarAsync(Empresa empresa)
        {
            const string sql = @"
                UPDATE dbo.empresas SET
                    razon_social          = @RazonSocial,
                    nombre_comercial      = @NombreComercial,
                    dir_matriz            = @DirMatriz,
                    obligado_contabilidad = @ObligadoContabilidad,
                    certificado_path      = @CertificadoPath,
                    cert_password         = @CertPassword,
                    updated_at            = @UpdatedAt
                WHERE ruc = @Ruc;";

            using (var con = _fabrica.Crear())
                await con.ExecuteAsync(sql, new
                {
                    empresa.RazonSocial,
                    empresa.NombreComercial,
                    empresa.DirMatriz,
                    empresa.ObligadoContabilidad,
                    empresa.CertificadoPath,
                    CertPassword = _protector.Cifrar(empresa.CertPassword),
                    empresa.UpdatedAt,
                    empresa.Ruc
                });
        }
    }
}
