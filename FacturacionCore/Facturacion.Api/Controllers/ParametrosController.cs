using System.Net;
using System.Threading.Tasks;
using System.Web.Http;
using Facturacion.Api.Auth;
using Facturacion.Api.Composition;
using Facturacion.Api.Models;
using Facturacion.Core;
using Facturacion.Core.Entidades;
using Facturacion.Core.Enums;
using Facturacion.Core.Interfaces.Repositorios;
using Facturacion.Core.Interfaces.Servicios;

namespace Facturacion.Api.Controllers
{
    [JwtAuthorize]
    [RoutePrefix("api/v1/parametros")]
    public class ParametrosController : ApiController
    {
        private readonly IParametrosRepositorio _parametros;
        private readonly ISecuencialesRepositorio _secuenciales;
        private readonly IAuditLogger _audit;

        public ParametrosController(IParametrosRepositorio parametros, ISecuencialesRepositorio secuenciales, IAuditLogger audit)
        {
            _parametros = parametros;
            _secuenciales = secuenciales;
            _audit = audit;
        }

        [HttpPost, Route("")]
        public async Task<IHttpActionResult> Crear(CrearParametrosRequest req)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            if (req == null || string.IsNullOrWhiteSpace(req.EmpresaRuc))
                return this.DesdeError(Errores.Parametros.EmpresaRucRequerido);

            if (await _parametros.ObtenerPorRucAsync(req.EmpresaRuc) != null)
                return this.DesdeError(Errores.Parametros.YaExisten);

            var parametros = ParametrosFacturacion.Crear(
                req.EmpresaRuc, req.Estab, req.PtoEmi, req.DireccionEstablecimiento, req.ContribuyenteEspecial);

            await _parametros.AgregarAsync(parametros);

            // Semilla del secuencial de facturas (arranca en 0) si aún no existe.
            var existente = await _secuenciales.ObtenerAsync(req.EmpresaRuc, req.Estab, req.PtoEmi, TipoDocumentoSri.Factura);
            if (existente == null)
                await _secuenciales.AgregarAsync(
                    SecuencialSri.Crear(req.EmpresaRuc, req.Estab, req.PtoEmi, TipoDocumentoSri.Factura));

            _audit.Registrar(new RegistroAuditoria
            {
                Tipo = EventoAuditoria.ParametrosCreados,
                Cliente = this.ClienteActual(),
                Ruc = req.EmpresaRuc,
                Ip = this.IpActual(),
                Exito = true
            });

            return Content(HttpStatusCode.Created, new { id = parametros.Id, empresaRuc = parametros.EmpresaRuc });
        }

        [HttpGet, Route("{ruc}")]
        public async Task<IHttpActionResult> Obtener(string ruc)
        {
            var parametros = await _parametros.ObtenerPorRucAsync(ruc);
            if (parametros == null) return this.DesdeError(Errores.Parametros.NoEncontrados);

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
