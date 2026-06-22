using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ErrorOr;
using Facturacion.Core.CasosDeUso.Comun;
using Facturacion.Core.Entidades;
using Facturacion.Core.Enums;
using Facturacion.Core.Interfaces.Repositorios;
using Facturacion.Core.Interfaces.Servicios;
using Facturacion.Core.Metodos;

namespace Facturacion.Core.CasosDeUso.Facturas
{
    public class ComandoEmitirFactura
    {
        public string EmpresaRuc { get; set; }
        public string Estab { get; set; }
        public string PtoEmi { get; set; }
        public Ambiente Ambiente { get; set; }
        public DateTime FechaEmision { get; set; }
        public string RazonSocialComprador { get; set; }
        public string IdentificacionComprador { get; set; }
        public TipoIdentificacion TipoIdentificacion { get; set; }
        public string DireccionComprador { get; set; }
        public List<FacturaDetalle> Detalles { get; set; }
        public List<FormaPago> FormasPago { get; set; }
        public List<InfoAdicional> InfoAdicional { get; set; }
        public string GuiaRemision { get; set; }
    }

    public class ResultadoEmitirFactura
    {
        public int Id { get; set; }
        public string ClaveAcceso { get; set; }
        public EstadoSri EstadoSri { get; set; }
        public string NumeroAutorizacion { get; set; }
    }

    public class EmitirFactura
    {
        private readonly IFacturasRepositorio _facturas;
        private readonly IEmpresasRepositorio _empresas;
        private readonly IParametrosRepositorio _parametros;
        private readonly ISecuencialesRepositorio _secuenciales;
        private readonly IServicioXml _xml;
        private readonly OrquestadorEmision _orquestador;

        public EmitirFactura(
            IFacturasRepositorio facturas,
            IEmpresasRepositorio empresas,
            IParametrosRepositorio parametros,
            ISecuencialesRepositorio secuenciales,
            IServicioXml xml,
            OrquestadorEmision orquestador)
        {
            _facturas = facturas;
            _empresas = empresas;
            _parametros = parametros;
            _secuenciales = secuenciales;
            _xml = xml;
            _orquestador = orquestador;
        }

        public async Task<ErrorOr<ResultadoEmitirFactura>> EjecutarAsync(ComandoEmitirFactura cmd)
        {
            var empresa = await _empresas.ObtenerPorRucAsync(cmd.EmpresaRuc);
            if (empresa == null) return Errores.Empresa.NoEncontrada;

            var parametros = await _parametros.ObtenerPorRucAsync(cmd.EmpresaRuc);
            if (parametros == null) return Errores.Parametros.NoEncontrados;

            // Secuencial atómico (UPDATE ... OUTPUT)
            int secuencialNum;
            try
            {
                secuencialNum = await _secuenciales.IncrementarYObtenerAsync(
                    cmd.EmpresaRuc, cmd.Estab, cmd.PtoEmi, TipoDocumentoSri.Factura);
            }
            catch
            {
                return Errores.Secuencial.NoConfigurado;
            }

            var secuencial = secuencialNum.ToString("D9");
            var codigoNumerico = new Random().Next(10000000, 99999999).ToString();

            var claveAcceso = GeneradorClaveAcceso.Generar(
                cmd.FechaEmision,
                TipoDocumentoSri.Factura,
                cmd.EmpresaRuc,
                cmd.Ambiente,
                cmd.Estab,
                cmd.PtoEmi,
                secuencial,
                codigoNumerico);

            // Verificar duplicado (doble seguridad, el índice filtrado en BD es la garantía real)
            var duplicado = await _facturas.ExisteSecuencialActivoAsync(
                cmd.EmpresaRuc, cmd.Estab, cmd.PtoEmi, secuencial, cmd.Ambiente);
            if (duplicado) return Errores.Factura.SecuencialDuplicado;

            // Calcular totales desde detalles
            decimal totalSinImpuestos = 0, totalIva = 0, totalIce = 0, totalDescuento = 0;
            foreach (var detalle in cmd.Detalles)
            {
                totalSinImpuestos += detalle.PrecioTotalSinImpuesto;
                totalIce += detalle.IceValor;
                totalIva += detalle.IvaValor;
                totalDescuento += detalle.Descuento;
            }

            var factura = Factura.Crear(
                cmd.EmpresaRuc, claveAcceso, cmd.Ambiente,
                cmd.Estab, cmd.PtoEmi, secuencial, cmd.FechaEmision,
                cmd.RazonSocialComprador, cmd.IdentificacionComprador, cmd.TipoIdentificacion,
                cmd.DireccionComprador,
                totalSinImpuestos, totalDescuento, totalIva, totalIce,
                totalSinImpuestos + totalIce + totalIva,
                cmd.Detalles, cmd.FormasPago, cmd.InfoAdicional, cmd.GuiaRemision);

            // INSERT antes de llamar al SRI
            await _facturas.AgregarAsync(factura);

            string xmlSinFirmar;
            try
            {
                xmlSinFirmar = _xml.GenerarXmlFactura(factura, empresa, parametros);
            }
            catch
            {
                // No dejamos que un fallo de serialización escape como 500 genérico.
                return Errores.Xml.ErrorGeneracion;
            }

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

            var resultado = await _orquestador.EjecutarAsync(new ParametrosEmision<Factura>
            {
                Documento = factura,
                XmlSinFirmar = xmlSinFirmar,
                CertificadoP12 = certBytes,
                CertPassword = empresa.CertPassword,
                Empresa = empresa,
                ParametrosFacturacion = parametros,
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
