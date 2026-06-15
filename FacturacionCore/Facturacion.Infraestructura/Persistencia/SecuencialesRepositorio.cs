using System.Threading.Tasks;
using Dapper;
using Facturacion.Core.Entidades;
using Facturacion.Core.Enums;
using Facturacion.Core.Interfaces.Repositorios;

namespace Facturacion.Infraestructura.Persistencia
{
    public class SecuencialesRepositorio : ISecuencialesRepositorio
    {
        private readonly IFabricaConexion _fabrica;

        public SecuencialesRepositorio(IFabricaConexion fabrica)
        {
            _fabrica = fabrica;
        }

        public async Task<SecuencialSri> ObtenerAsync(string empresaRuc, string estab, string ptoEmi, TipoDocumentoSri tipoDocumento)
        {
            const string sql = @"
                SELECT id, empresa_ruc, estab, pto_emi, tipo_documento,
                       ultimo_secuencial, created_at, updated_at
                FROM dbo.secuenciales_sri
                WHERE empresa_ruc = @empresaRuc AND estab = @estab
                  AND pto_emi = @ptoEmi AND tipo_documento = @tipoDocumento;";

            using (var con = _fabrica.Crear())
                return await con.QueryFirstOrDefaultAsync<SecuencialSri>(sql, new
                {
                    empresaRuc,
                    estab,
                    ptoEmi,
                    tipoDocumento = (int)tipoDocumento
                });
        }

        // Incremento atómico: UPDATE ... OUTPUT en una sola sentencia.
        // Si no existe el secuencial configurado, OUTPUT no devuelve filas y
        // QuerySingleAsync lanza — el caso de uso lo traduce a Secuencial.NoConfigurado.
        public async Task<int> IncrementarYObtenerAsync(string empresaRuc, string estab, string ptoEmi, TipoDocumentoSri tipoDocumento)
        {
            const string sql = @"
                UPDATE dbo.secuenciales_sri
                SET ultimo_secuencial = ultimo_secuencial + 1,
                    updated_at        = SYSUTCDATETIME()
                OUTPUT INSERTED.ultimo_secuencial
                WHERE empresa_ruc = @empresaRuc AND estab = @estab
                  AND pto_emi = @ptoEmi AND tipo_documento = @tipoDocumento;";

            using (var con = _fabrica.Crear())
                return await con.QuerySingleAsync<int>(sql, new
                {
                    empresaRuc,
                    estab,
                    ptoEmi,
                    tipoDocumento = (int)tipoDocumento
                });
        }

        public async Task AgregarAsync(SecuencialSri secuencial)
        {
            const string sql = @"
                INSERT INTO dbo.secuenciales_sri
                    (empresa_ruc, estab, pto_emi, tipo_documento,
                     ultimo_secuencial, created_at, updated_at)
                OUTPUT INSERTED.id
                VALUES
                    (@EmpresaRuc, @Estab, @PtoEmi, @TipoDocumento,
                     @UltimoSecuencial, @CreatedAt, @UpdatedAt);";

            using (var con = _fabrica.Crear())
            {
                var id = await con.ExecuteScalarAsync<int>(sql, new
                {
                    secuencial.EmpresaRuc,
                    secuencial.Estab,
                    secuencial.PtoEmi,
                    TipoDocumento = (int)secuencial.TipoDocumento,
                    secuencial.UltimoSecuencial,
                    secuencial.CreatedAt,
                    secuencial.UpdatedAt
                });
                secuencial.AsignarId(id);
            }
        }

        public async Task ActualizarAsync(SecuencialSri secuencial)
        {
            const string sql = @"
                UPDATE dbo.secuenciales_sri SET
                    ultimo_secuencial = @UltimoSecuencial,
                    updated_at        = @UpdatedAt
                WHERE id = @Id;";

            using (var con = _fabrica.Crear())
                await con.ExecuteAsync(sql, new
                {
                    secuencial.UltimoSecuencial,
                    secuencial.UpdatedAt,
                    secuencial.Id
                });
        }
    }
}
