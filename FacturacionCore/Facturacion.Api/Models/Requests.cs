using System;
using System.Collections.Generic;
using FluentValidation.Attributes;
using Facturacion.Api.Validators;
using Facturacion.Core.Enums;

namespace Facturacion.Api.Models
{
    // ─── Facturas ───────────────────────────────────────────────────────────────

    [Validator(typeof(EmitirFacturaValidator))]
    public class EmitirFacturaRequest
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
        public string GuiaRemision { get; set; }
        public List<DetalleRequest> Detalles { get; set; }
        public List<FormaPagoRequest> FormasPago { get; set; }
        public List<InfoAdicionalRequest> InfoAdicional { get; set; }
    }

    public class DetalleRequest
    {
        public string CodigoPrincipal { get; set; }
        public string CodigoAuxiliar { get; set; }
        public string Descripcion { get; set; }
        public decimal Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal Descuento { get; set; }
        public CodigoIva CodigoIva { get; set; }
        public decimal IceValor { get; set; }
    }

    public class FormaPagoRequest
    {
        public string Codigo { get; set; }
        public decimal Total { get; set; }
        public int? Plazo { get; set; }
        public string UnidadTiempo { get; set; }
    }

    public class InfoAdicionalRequest
    {
        public string Nombre { get; set; }
        public string Valor { get; set; }
    }

    // ─── Empresas ─────────────────────────────────────────────────────────────

    public class CrearEmpresaRequest
    {
        public string Ruc { get; set; }
        public string RazonSocial { get; set; }
        public string NombreComercial { get; set; }
        public string DirMatriz { get; set; }
        public bool ObligadoContabilidad { get; set; }
        public string CertificadoPath { get; set; }
        public string CertPassword { get; set; }
    }

    // ─── Parámetros ─────────────────────────────────────────────────────────────

    public class CrearParametrosRequest
    {
        public string EmpresaRuc { get; set; }
        public string Estab { get; set; }
        public string PtoEmi { get; set; }
        public string DireccionEstablecimiento { get; set; }
        public string ContribuyenteEspecial { get; set; }
    }

    // ─── Auth ─────────────────────────────────────────────────────────────────

    public class TokenRequest
    {
        public string Cliente { get; set; }
        public string Secret { get; set; }
    }
}
