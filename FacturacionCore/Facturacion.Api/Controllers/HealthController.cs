using System;
using System.Data;
using System.Data.Common;
using System.Net;
using System.Threading.Tasks;
using System.Web.Http;
using Facturacion.Infraestructura.Persistencia;
using Serilog;

namespace Facturacion.Api.Controllers
{
    // Health check público (sin JWT): verifica que la API responde y que la BD es alcanzable.
    // Devuelve 200 si todo está sano, 503 Service Unavailable si la BD no responde.
    [RoutePrefix("")]
    public class HealthController : ApiController
    {
        private readonly IFabricaConexion _conexion;

        public HealthController(IFabricaConexion conexion)
        {
            _conexion = conexion;
        }

        [HttpGet, Route("health")]
        public async Task<IHttpActionResult> Health()
        {
            bool bdSana = await VerificarBaseDatosAsync();

            var cuerpo = new
            {
                status = bdSana ? "Healthy" : "Unhealthy",
                checks = new { database = bdSana ? "up" : "down" },
                timestamp = DateTime.UtcNow.ToString("o")
            };

            var estado = bdSana ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable;
            return Content(estado, cuerpo);
        }

        private async Task<bool> VerificarBaseDatosAsync()
        {
            try
            {
                using (var con = _conexion.Crear())
                {
                    var dbCon = con as DbConnection;
                    if (dbCon != null) await dbCon.OpenAsync();
                    else con.Open();

                    using (var cmd = con.CreateCommand())
                    {
                        cmd.CommandText = "SELECT 1";
                        cmd.CommandType = CommandType.Text;
                        cmd.CommandTimeout = 5;

                        var dbCmd = cmd as DbCommand;
                        var resultado = dbCmd != null
                            ? await dbCmd.ExecuteScalarAsync()
                            : cmd.ExecuteScalar();

                        return resultado != null && Convert.ToInt32(resultado) == 1;
                    }
                }
            }
            catch (Exception ex)
            {
                // No filtramos el detalle al cliente; queda en el log para diagnóstico.
                Log.ForContext<HealthController>().Warning(ex, "Health check: la BD no respondió.");
                return false;
            }
        }
    }
}
