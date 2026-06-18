using System.Net;
using System.Threading.Tasks;
using System.Web.Http;
using Facturacion.Api.Auth;
using Facturacion.Api.Composition;
using Facturacion.Api.Models;
using Facturacion.Core;
using Facturacion.Core.Entidades;
using Facturacion.Core.Interfaces.Repositorios;

namespace Facturacion.Api.Controllers
{
    [JwtAuthorize]
    [RoutePrefix("api/v1/empresas")]
    public class EmpresasController : ApiController
    {
        private readonly IEmpresasRepositorio _empresas;

        public EmpresasController(IEmpresasRepositorio empresas)
        {
            _empresas = empresas;
        }

        [HttpPost, Route("")]
        public async Task<IHttpActionResult> Crear(CrearEmpresaRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.Ruc))
                return this.DesdeError(Errores.Empresa.NoEncontrada);

            if (await _empresas.ExistePorRucAsync(req.Ruc))
                return this.DesdeError(Errores.Empresa.RucDuplicado);

            var empresa = Empresa.Crear(
                req.Ruc, req.RazonSocial, req.NombreComercial, req.DirMatriz,
                req.ObligadoContabilidad, req.CertificadoPath, req.CertPassword);

            await _empresas.AgregarAsync(empresa);

            return Content(HttpStatusCode.Created, new { id = empresa.Id, ruc = empresa.Ruc });
        }

        [HttpGet, Route("{ruc}")]
        public async Task<IHttpActionResult> Obtener(string ruc)
        {
            var empresa = await _empresas.ObtenerPorRucAsync(ruc);
            if (empresa == null) return NotFound();

            return Ok(new
            {
                id = empresa.Id,
                ruc = empresa.Ruc,
                razonSocial = empresa.RazonSocial,
                nombreComercial = empresa.NombreComercial,
                dirMatriz = empresa.DirMatriz,
                obligadoContabilidad = empresa.ObligadoContabilidad
            });
        }
    }
}
