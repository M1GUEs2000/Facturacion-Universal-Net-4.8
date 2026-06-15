using System.Threading.Tasks;
using Dapper;
using Facturacion.Core.Entidades;
using Facturacion.Core.Interfaces.Repositorios;

namespace Facturacion.Infraestructura.Persistencia
{
    public class ParametrosRepositorio : IParametrosRepositorio
    {
        private readonly IFabricaConexion _fabrica;

        public ParametrosRepositorio(IFabricaConexion fabrica)
        {
            _fabrica = fabrica;
        }

        public async Task<ParametrosFacturacion> ObtenerPorRucAsync(string empresaRuc)
        {
            const string sql = @"
                SELECT id, empresa_ruc, estab, pto_emi,
                       direccion_establecimiento, contribuyente_especial,
                       created_at, updated_at
                FROM dbo.parametros_facturacion
                WHERE empresa_ruc = @empresaRuc;";

            using (var con = _fabrica.Crear())
                return await con.QueryFirstOrDefaultAsync<ParametrosFacturacion>(sql, new { empresaRuc });
        }

        public async Task AgregarAsync(ParametrosFacturacion parametros)
        {
            const string sql = @"
                INSERT INTO dbo.parametros_facturacion
                    (empresa_ruc, estab, pto_emi, direccion_establecimiento,
                     contribuyente_especial, created_at, updated_at)
                OUTPUT INSERTED.id
                VALUES
                    (@EmpresaRuc, @Estab, @PtoEmi, @DireccionEstablecimiento,
                     @ContribuyenteEspecial, @CreatedAt, @UpdatedAt);";

            using (var con = _fabrica.Crear())
            {
                var id = await con.ExecuteScalarAsync<int>(sql, new
                {
                    parametros.EmpresaRuc,
                    parametros.Estab,
                    parametros.PtoEmi,
                    parametros.DireccionEstablecimiento,
                    parametros.ContribuyenteEspecial,
                    parametros.CreatedAt,
                    parametros.UpdatedAt
                });
                parametros.AsignarId(id);
            }
        }

        public async Task ActualizarAsync(ParametrosFacturacion parametros)
        {
            const string sql = @"
                UPDATE dbo.parametros_facturacion SET
                    estab                     = @Estab,
                    pto_emi                   = @PtoEmi,
                    direccion_establecimiento = @DireccionEstablecimiento,
                    contribuyente_especial    = @ContribuyenteEspecial,
                    updated_at                = @UpdatedAt
                WHERE empresa_ruc = @EmpresaRuc;";

            using (var con = _fabrica.Crear())
                await con.ExecuteAsync(sql, new
                {
                    parametros.Estab,
                    parametros.PtoEmi,
                    parametros.DireccionEstablecimiento,
                    parametros.ContribuyenteEspecial,
                    parametros.UpdatedAt,
                    parametros.EmpresaRuc
                });
        }
    }
}
