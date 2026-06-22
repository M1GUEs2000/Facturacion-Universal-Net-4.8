using System.Net;
using System.Threading.Tasks;
using System.Web.Http;
using Facturacion.Api.Auth;
using Facturacion.Api.Composition;
using Facturacion.Api.Models;
using Facturacion.Core;
using Facturacion.Core.Entidades;
using Facturacion.Core.Interfaces.Repositorios;
using Facturacion.Core.Interfaces.Servicios;

namespace Facturacion.Api.Controllers
{
    [JwtAuthorize]
    [RoutePrefix("api/v1/empresas")]
    public class EmpresasController : ApiController
    {
        private readonly IEmpresasRepositorio _empresas;
        private readonly IAuditLogger _audit;

        public EmpresasController(IEmpresasRepositorio empresas, IAuditLogger audit)
        {
            _empresas = empresas;
            _audit = audit;
        }

        [HttpPost, Route("")]
        public async Task<IHttpActionResult> Crear(CrearEmpresaRequest req)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            if (req == null || string.IsNullOrWhiteSpace(req.Ruc))
            {
                Auditar(req?.Ruc, false, Errores.Empresa.RucRequerido.Code);
                return this.DesdeError(Errores.Empresa.RucRequerido);
            }

            if (await _empresas.ExistePorRucAsync(req.Ruc))
            {
                Auditar(req.Ruc, false, Errores.Empresa.RucDuplicado.Code);
                return this.DesdeError(Errores.Empresa.RucDuplicado);
            }

            var empresa = Empresa.Crear(
                req.Ruc, req.RazonSocial, req.NombreComercial, req.DirMatriz,
                req.ObligadoContabilidad, req.CertificadoPath, req.CertPassword);

            await _empresas.AgregarAsync(empresa);

            Auditar(empresa.Ruc, true, null);
            return Content(HttpStatusCode.Created, new { id = empresa.Id, ruc = empresa.Ruc });
        }

        private void Auditar(string ruc, bool exito, string codigoError)
        {
            _audit.Registrar(new RegistroAuditoria
            {
                Tipo = EventoAuditoria.EmpresaCreada,
                Cliente = this.ClienteActual(),
                Ruc = ruc,
                Ip = this.IpActual(),
                Exito = exito,
                CodigoError = codigoError
            });
        }

        [HttpGet, Route("{ruc}")]
        public async Task<IHttpActionResult> Obtener(string ruc)
        {
            var empresa = await _empresas.ObtenerPorRucAsync(ruc);
            if (empresa == null) return this.DesdeError(Errores.Empresa.NoEncontrada);

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
