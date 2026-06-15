using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using Facturacion.Core.Entidades;
using Facturacion.Core.Enums;
using Facturacion.Core.Interfaces.Servicios;
using Facturacion.Infraestructura.Servicios.Xml.Modelos;
using Microsoft.Extensions.Logging;

namespace Facturacion.Infraestructura.Servicios.Xml
{
    public class ServicioXml : IServicioXml
    {
        private static readonly XmlSerializerNamespaces EmptyNs = BuildEmptyNs();
        private static readonly ConcurrentDictionary<Type, XmlSerializer> SerializerCache =
            new ConcurrentDictionary<Type, XmlSerializer>();

        private readonly ILogger<ServicioXml> _logger;

        public ServicioXml(ILogger<ServicioXml> logger)
        {
            _logger = logger;
        }

        public string GenerarXmlFactura(Factura factura, Empresa empresa, ParametrosFacturacion parametros)
        {
            try
            {
                return Serializar(MapearFactura(factura, empresa, parametros));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error serializando XML de factura {ClaveAcceso}", factura.ClaveAcceso);
                throw;
            }
        }

        // ─── Mapping ──────────────────────────────────────────────────────────────

        private static XmlFactura MapearFactura(Factura f, Empresa e, ParametrosFacturacion p)
        {
            return new XmlFactura
            {
                InfoTributaria = BuildInfoTributaria(e, f.ClaveAcceso, TipoDocumentoSri.Factura,
                    f.Ambiente, f.Estab, f.PtoEmi, f.Secuencial),
                InfoFactura = new XmlInfoFactura
                {
                    FechaEmision               = f.FechaEmision.ToString("dd/MM/yyyy"),
                    DirEstablecimiento         = p != null ? p.DireccionEstablecimiento : null,
                    ObligadoContabilidad       = e.ObligadoContabilidad ? "SI" : "NO",
                    TipoIdentificacionComprador = ((int)f.TipoIdentificacionComprador).ToString("D2"),
                    GuiaRemision               = f.GuiaRemision,
                    RazonSocialComprador       = f.RazonSocialComprador,
                    IdentificacionComprador    = f.IdentificacionComprador,
                    DireccionComprador         = f.DireccionComprador,
                    TotalSinImpuestos          = M(f.TotalSinImpuestos),
                    TotalDescuento             = M(f.TotalDescuento),
                    TotalConImpuestos          = BuildTotalImpuestos(f.Detalles),
                    ImporteTotal               = M(f.ImporteTotal),
                    Moneda                     = f.Moneda ?? "DOLAR",
                    Pagos                      = f.FormasPago.Select(fp => new XmlPago
                    {
                        FormaPago   = fp.Codigo,
                        Total       = M(fp.Total),
                        Plazo       = fp.Plazo.HasValue ? fp.Plazo.Value.ToString() : null,
                        UnidadTiempo = fp.UnidadTiempo
                    }).ToList()
                },
                Detalles      = f.Detalles.Select(MapDetalle).ToList(),
                InfoAdicional = f.InfoAdicional.Select(ia => new XmlCampoAdicional
                {
                    Nombre = ia.Nombre,
                    Valor  = ia.Valor
                }).ToList()
            };
        }

        // ─── Builders ─────────────────────────────────────────────────────────────

        private static XmlInfoTributaria BuildInfoTributaria(
            Empresa e, string claveAcceso, TipoDocumentoSri tipoDoc,
            Ambiente ambiente, string estab, string ptoEmi, string secuencial)
        {
            return new XmlInfoTributaria
            {
                Ambiente       = ((int)ambiente).ToString(),
                TipoEmision    = "1",
                RazonSocial    = e.RazonSocial,
                NombreComercial = e.NombreComercial,
                Ruc            = e.Ruc,
                ClaveAcceso    = claveAcceso,
                CodDoc         = ((int)tipoDoc).ToString("D2"),
                Estab          = estab,
                PtoEmi         = ptoEmi,
                Secuencial     = secuencial,
                DirMatriz      = e.DirMatriz
            };
        }

        // ICE siempre antes que IVA — requerido por el XSD del SRI
        private static List<XmlTotalImpuesto> BuildTotalImpuestos(List<FacturaDetalle> detalles)
        {
            var lista = new List<XmlTotalImpuesto>();

            // ICE (código 3) — solo si hay valores de ICE
            var detallesConIce = detalles.Where(d => d.IceValor > 0).ToList();
            if (detallesConIce.Any())
            {
                lista.Add(new XmlTotalImpuesto
                {
                    Codigo           = "3",
                    CodigoPorcentaje = "0",
                    BaseImponible    = M(detallesConIce.Sum(d => d.PrecioTotalSinImpuesto)),
                    Tarifa           = "0.00",
                    Valor            = M(detallesConIce.Sum(d => d.IceValor))
                });
            }

            // IVA (código 2) — agrupado por código de IVA
            foreach (var grupo in detalles.GroupBy(d => d.CodigoIva))
            {
                lista.Add(new XmlTotalImpuesto
                {
                    Codigo           = "2",
                    CodigoPorcentaje = ((int)grupo.Key).ToString(),
                    BaseImponible    = M(grupo.Sum(d => d.IvaBase)),
                    Tarifa           = M(TarifaIva(grupo.Key)),
                    Valor            = M(grupo.Sum(d => d.IvaValor))
                });
            }

            return lista;
        }

        private static XmlDetalleFactura MapDetalle(FacturaDetalle d)
        {
            return new XmlDetalleFactura
            {
                CodigoPrincipal        = d.CodigoPrincipal,
                CodigoAuxiliar         = d.CodigoAuxiliar,
                Descripcion            = d.Descripcion,
                Cantidad               = M(d.Cantidad),
                PrecioUnitario         = PU(d.PrecioUnitario),
                Descuento              = M(d.Descuento),
                PrecioTotalSinImpuesto = M(d.PrecioTotalSinImpuesto),
                Impuestos              = BuildImpuestosDetalle(d)
            };
        }

        // ICE siempre antes que IVA en el detalle
        private static List<XmlImpuestoDetalle> BuildImpuestosDetalle(FacturaDetalle d)
        {
            var impuestos = new List<XmlImpuestoDetalle>();

            if (d.IceValor > 0)
            {
                impuestos.Add(new XmlImpuestoDetalle
                {
                    Codigo           = "3",
                    CodigoPorcentaje = "0",
                    Tarifa           = "0.00",
                    BaseImponible    = M(d.PrecioTotalSinImpuesto),
                    Valor            = M(d.IceValor)
                });
            }

            impuestos.Add(new XmlImpuestoDetalle
            {
                Codigo           = "2",
                CodigoPorcentaje = ((int)d.CodigoIva).ToString(),
                Tarifa           = M(TarifaIva(d.CodigoIva)),
                BaseImponible    = M(d.IvaBase),
                Valor            = M(d.IvaValor)
            });

            return impuestos;
        }

        // ─── Helpers ──────────────────────────────────────────────────────────────

        private static decimal TarifaIva(CodigoIva codigo)
        {
            switch (codigo)
            {
                case CodigoIva.Iva12:            return 12m;
                case CodigoIva.Iva14:            return 14m;
                case CodigoIva.Iva15:            return 15m;
                case CodigoIva.Iva5:             return 5m;
                case CodigoIva.Iva8Diferenciado: return 8m;
                case CodigoIva.Iva13:            return 13m;
                default:                         return 0m;
            }
        }

        private static string M(decimal v)  => v.ToString("0.00", CultureInfo.InvariantCulture);
        private static string PU(decimal v) => v.ToString("0.000000", CultureInfo.InvariantCulture);

        // ─── Serialización ────────────────────────────────────────────────────────

        private static string Serializar<T>(T modelo)
        {
            var serializer = SerializerCache.GetOrAdd(typeof(T), t => new XmlSerializer(t));
            var settings = new XmlWriterSettings
            {
                Encoding             = new UTF8Encoding(false),
                Indent               = false,
                OmitXmlDeclaration   = false
            };

            using (var ms = new MemoryStream())
            using (var writer = XmlWriter.Create(ms, settings))
            {
                serializer.Serialize(writer, modelo, EmptyNs);
                writer.Flush();
                return Encoding.UTF8.GetString(ms.ToArray());
            }
        }

        private static XmlSerializerNamespaces BuildEmptyNs()
        {
            var ns = new XmlSerializerNamespaces();
            ns.Add("", "");
            return ns;
        }
    }
}
