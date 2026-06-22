using System.Threading.Tasks;
using ErrorOr;
using Facturacion.Core.CasosDeUso.Comun;
using Facturacion.Core.Entidades;
using Facturacion.Core.Interfaces.Repositorios;
using Facturacion.Core.Interfaces.Servicios;

namespace Facturacion.Core.CasosDeUso.Facturas
{
    public class ComandoReintentarEmisionFactura
    {
        public int FacturaId { get; set; }
    }

    public class ReintentarEmisionFactura
    {
        private readonly IFacturasRepositorio _facturas;
        private readonly IEmpresasRepositorio _empresas;
        private readonly IParametrosRepositorio _parametros;
        private readonly IServicioXml _xml;
        private readonly OrquestadorReintento _orquestador;

        public ReintentarEmisionFactura(
            IFacturasRepositorio facturas,
            IEmpresasRepositorio empresas,
            IParametrosRepositorio parametros,
            IServicioXml xml,
            OrquestadorReintento orquestador)
        {
            _facturas = facturas;
            _empresas = empresas;
            _parametros = parametros;
            _xml = xml;
            _orquestador = orquestador;
        }

        public async Task<ErrorOr<ResultadoEmitirFactura>> EjecutarAsync(ComandoReintentarEmisionFactura cmd)
        {
            var factura = await _facturas.ObtenerPorIdAsync(cmd.FacturaId);
            if (factura == null) return Errores.Factura.NoEncontrada;

            var empresa = await _empresas.ObtenerPorRucAsync(factura.EmpresaRuc);
            if (empresa == null) return Errores.Empresa.NoEncontrada;

            var parametros = await _parametros.ObtenerPorRucAsync(factura.EmpresaRuc);
            if (parametros == null) return Errores.Parametros.NoEncontrados;

            byte[] certBytes;
            try
            {
                certBytes = System.IO.File.ReadAllBytes(empresa.CertificadoPath);
            }
            catch
            {
                // Ruta inválida / archivo ausente / sin permisos → error tipado, no 500.
                return Errores.Empresa.CertificadoNoAccesible;
            }

            var resultado = await _orquestador.EjecutarAsync(new ParametrosReintento<Factura>
            {
                Documento = factura,
                CertificadoP12 = certBytes,
                CertPassword = empresa.CertPassword,
                Empresa = empresa,
                ParametrosFacturacion = parametros,
                GenerarXmlSinFirmar = () => _xml.GenerarXmlFactura(factura, empresa, parametros),
                Persistir = f => _facturas.ActualizarAsync(f)
            });

            if (resultado.IsError) return resultado.FirstError;

            return new ResultadoEmitirFactura
            {
                Id = resultado.Value.Id,
                ClaveAcceso = resultado.Value.ClaveAcceso,
                EstadoSri = resultado.Value.EstadoSri,
                NumeroAutorizacion = resultado.Value.NumeroAutorizacion
            };
        }
    }
}
