using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using System.Web.Http;
using Facturacion.Api.Auth;
using Facturacion.Api.Composition;
using Facturacion.Api.Models;
using Facturacion.Core.CasosDeUso.Facturas;
using Facturacion.Core.Entidades;
using Facturacion.Core.Interfaces.Repositorios;
using Facturacion.Core.Interfaces.Servicios;

namespace Facturacion.Api.Controllers
{
    [JwtAuthorize]
    [RoutePrefix("api/v1/facturas")]
    public class FacturasController : ApiController
    {
        private readonly EmitirFactura _emitir;
        private readonly ReintentarEmisionFactura _reintentar;
        private readonly EnviarCorreoFactura _correo;
        private readonly IFacturasRepositorio _facturas;
        private readonly IAuditLogger _audit;

        public FacturasController(EmitirFactura emitir, ReintentarEmisionFactura reintentar,
            EnviarCorreoFactura correo, IFacturasRepositorio facturas, IAuditLogger audit)
        {
            _emitir = emitir;
            _reintentar = reintentar;
            _correo = correo;
            _facturas = facturas;
            _audit = audit;
        }

        [HttpPost, Route("")]
        public async Task<IHttpActionResult> Emitir(EmitirFacturaRequest req)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var cmd = new ComandoEmitirFactura
            {
                EmpresaRuc = req.EmpresaRuc,
                Estab = req.Estab,
                PtoEmi = req.PtoEmi,
                Ambiente = req.Ambiente,
                FechaEmision = req.FechaEmision,
                RazonSocialComprador = req.RazonSocialComprador,
                IdentificacionComprador = req.IdentificacionComprador,
                TipoIdentificacion = req.TipoIdentificacion,
                DireccionComprador = req.DireccionComprador,
                GuiaRemision = req.GuiaRemision,
                Detalles = MapearDetalles(req.Detalles),
                FormasPago = MapearFormasPago(req.FormasPago),
                InfoAdicional = MapearInfoAdicional(req.InfoAdicional)
            };

            var resultado = await _emitir.EjecutarAsync(cmd);

            _audit.Registrar(new RegistroAuditoria
            {
                Tipo = EventoAuditoria.FacturaEmitida,
                Cliente = this.ClienteActual(),
                Ruc = req.EmpresaRuc,
                Ip = this.IpActual(),
                Exito = !resultado.IsError,
                CodigoError = resultado.IsError ? resultado.FirstError.Code : null,
                Detalle = resultado.IsError ? null : resultado.Value.ClaveAcceso
            });

            return this.Responder(resultado, HttpStatusCode.Created, r => new
            {
                id = r.Id,
                claveAcceso = r.ClaveAcceso,
                estadoSri = r.EstadoSri.ToString(),
                numeroAutorizacion = r.NumeroAutorizacion
            });
        }

        [HttpPost, Route("{id:int}/reintentar")]
        public async Task<IHttpActionResult> Reintentar(int id)
        {
            var resultado = await _reintentar.EjecutarAsync(new ComandoReintentarEmisionFactura { FacturaId = id });

            _audit.Registrar(new RegistroAuditoria
            {
                Tipo = EventoAuditoria.FacturaReintentada,
                Cliente = this.ClienteActual(),
                Ip = this.IpActual(),
                Exito = !resultado.IsError,
                CodigoError = resultado.IsError ? resultado.FirstError.Code : null,
                Detalle = "facturaId=" + id
            });

            return this.Responder(resultado, HttpStatusCode.OK, r => new
            {
                id = r.Id,
                claveAcceso = r.ClaveAcceso,
                estadoSri = r.EstadoSri.ToString(),
                numeroAutorizacion = r.NumeroAutorizacion
            });
        }

        [HttpGet, Route("{id:int}")]
        public async Task<IHttpActionResult> Obtener(int id)
        {
            var factura = await _facturas.ObtenerPorIdAsync(id);
            if (factura == null) return NotFound();

            return Ok(new
            {
                id = factura.Id,
                claveAcceso = factura.ClaveAcceso,
                estadoSri = factura.EstadoSri.ToString(),
                numeroAutorizacion = factura.NumeroAutorizacion,
                secuencial = factura.Estab + "-" + factura.PtoEmi + "-" + factura.Secuencial,
                importeTotal = factura.ImporteTotal,
                xmlAutorizadoPath = factura.XmlAutorizadoPath,
                pdfPath = factura.PdfPath
            });
        }

        [HttpPost, Route("{id:int}/correo")]
        public async Task<IHttpActionResult> EnviarCorreo(int id, EnviarCorreoFacturaRequest req)
        {
            if (req == null || req.Destinatarios == null || req.Destinatarios.Count == 0)
                return BadRequest("Se requiere al menos un destinatario.");

            var cmd = new ComandoEnviarCorreoFactura
            {
                FacturaId = id,
                Destinatarios = req.Destinatarios
            };

            var resultado = await _correo.EjecutarAsync(cmd);

            _audit.Registrar(new RegistroAuditoria
            {
                Tipo = EventoAuditoria.FacturaEmitida,
                Cliente = this.ClienteActual(),
                Ip = this.IpActual(),
                Exito = !resultado.IsError,
                CodigoError = resultado.IsError ? resultado.FirstError.Code : null,
                Detalle = "correo facturaId=" + id
            });

            return this.Responder(resultado, HttpStatusCode.OK);
        }

        // ─── Mapeo request → dominio ─────────────────────────────────────────────

        private static List<FacturaDetalle> MapearDetalles(List<DetalleRequest> detalles)
        {
            var lista = new List<FacturaDetalle>();
            if (detalles == null) return lista;
            foreach (var d in detalles)
                lista.Add(FacturaDetalle.Crear(
                    d.CodigoPrincipal, d.Descripcion, d.Cantidad, d.PrecioUnitario,
                    d.Descuento, d.CodigoIva, d.IceValor, d.CodigoAuxiliar));
            return lista;
        }

        private static List<FormaPago> MapearFormasPago(List<FormaPagoRequest> formas)
        {
            var lista = new List<FormaPago>();
            if (formas == null) return lista;
            foreach (var f in formas)
                lista.Add(new FormaPago { Codigo = f.Codigo, Total = f.Total, Plazo = f.Plazo, UnidadTiempo = f.UnidadTiempo });
            return lista;
        }

        private static List<InfoAdicional> MapearInfoAdicional(List<InfoAdicionalRequest> info)
        {
            var lista = new List<InfoAdicional>();
            if (info == null) return lista;
            foreach (var i in info)
                lista.Add(new InfoAdicional { Nombre = i.Nombre, Valor = i.Valor });
            return lista;
        }
    }
}
