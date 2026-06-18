using System.Net;
using System.Threading.Tasks;
using System.Web.Http;
using Facturacion.Api.Auth;
using Facturacion.Api.Models;
using Facturacion.Core.Entidades;
using Facturacion.Core.Enums;
using Facturacion.Core.Interfaces.Repositorios;

namespace Facturacion.Api.Controllers
{
    [JwtAuthorize]
    [RoutePrefix("api/v1/parametros")]
    public class ParametrosController : ApiController
    {
        private readonly IParametrosRepositorio _parametros;
        private readonly ISecuencialesRepositorio _secuenciales;

        public ParametrosController(IParametrosRepositorio parametros, ISecuencialesRepositorio secuenciales)
        {
            _parametros = parametros;
            _secuenciales = secuenciales;
        }

        [HttpPost, Route("")]
        public async Task<IHttpActionResult> Crear(CrearParametrosRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.EmpresaRuc))
                return BadRequest("empresaRuc es obligatorio.");

            if (await _parametros.ObtenerPorRucAsync(req.EmpresaRuc) != null)
                return Conflict();

            var parametros = ParametrosFacturacion.Crear(
                req.EmpresaRuc, req.Estab, req.PtoEmi, req.DireccionEstablecimiento, req.ContribuyenteEspecial);

            await _parametros.AgregarAsync(parametros);

            // Semilla del secuencial de facturas (arranca en 0) si aún no existe.
            var existente = await _secuenciales.ObtenerAsync(req.EmpresaRuc, req.Estab, req.PtoEmi, TipoDocumentoSri.Factura);
            if (existente == null)
                await _secuenciales.AgregarAsync(
                    SecuencialSri.Crear(req.EmpresaRuc, req.Estab, req.PtoEmi, TipoDocumentoSri.Factura));

            return Content(HttpStatusCode.Created, new { id = parametros.Id, empresaRuc = parametros.EmpresaRuc });
        }

        [HttpGet, Route("{ruc}")]
        public async Task<IHttpActionResult> Obtener(string ruc)
        {
            var parametros = await _parametros.ObtenerPorRucAsync(ruc);
            if (parametros == null) return NotFound();

            return Ok(new
            {
                id = parametros.Id,
                empresaRuc = parametros.EmpresaRuc,
                estab = parametros.Estab,
                ptoEmi = parametros.PtoEmi,
                direccionEstablecimiento = parametros.DireccionEstablecimiento,
                contribuyenteEspecial = parametros.ContribuyenteEspecial
            });
        }
    }
}
