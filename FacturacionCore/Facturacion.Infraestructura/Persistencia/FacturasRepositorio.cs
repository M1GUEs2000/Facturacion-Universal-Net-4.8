using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;
using Dapper;
using Facturacion.Core.Entidades;
using Facturacion.Core.Enums;
using Facturacion.Core.Interfaces.Repositorios;

namespace Facturacion.Infraestructura.Persistencia
{
    public class FacturasRepositorio : IFacturasRepositorio
    {
        private readonly IFabricaConexion _fabrica;

        public FacturasRepositorio(IFabricaConexion fabrica)
        {
            _fabrica = fabrica;
        }

        // ─── Lectura ────────────────────────────────────────────────────────────

        public Task<Factura> ObtenerPorIdAsync(int id) =>
            ObtenerAsync("id = @id", new { id });

        public Task<Factura> ObtenerPorClaveAccesoAsync(string claveAcceso) =>
            ObtenerAsync("clave_acceso = @claveAcceso", new { claveAcceso });

        private async Task<Factura> ObtenerAsync(string filtro, object parametros)
        {
            string sqlFactura = @"
                SELECT id, empresa_ruc, clave_acceso, ambiente, estab, pto_emi, secuencial,
                       fecha_emision, estado_sri, tipo_identificacion_comprador,
                       identificacion_comprador, razon_social_comprador, direccion_comprador,
                       guia_remision, total_sin_impuestos, total_descuento, total_iva, total_ice,
                       importe_total, moneda, formas_pago, info_adicional, numero_autorizacion,
                       fecha_autorizacion, sri_respuesta, xml_firmado_path, xml_autorizado_path,
                       pdf_path, created_at, updated_at
                FROM dbo.facturas
                WHERE " + filtro + ";";

            const string sqlDetalles = @"
                SELECT id, factura_id, codigo_principal, codigo_auxiliar, descripcion,
                       cantidad, precio_unitario, descuento, precio_total_sin_impuesto,
                       codigo_iva, iva_base, iva_valor, ice_valor
                FROM dbo.facturas_detalle
                WHERE factura_id = @facturaId
                ORDER BY id;";

            using (var con = _fabrica.Crear())
            {
                var fila = await con.QueryFirstOrDefaultAsync<FilaFactura>(sqlFactura, parametros);
                if (fila == null) return null;

                var filasDetalle = await con.QueryAsync<FilaDetalle>(sqlDetalles, new { facturaId = fila.Id });

                var detalles = new List<FacturaDetalle>();
                foreach (var d in filasDetalle)
                    detalles.Add(FacturaDetalle.Reconstituir(
                        d.Id, d.FacturaId, d.CodigoPrincipal, d.CodigoAuxiliar, d.Descripcion,
                        d.Cantidad, d.PrecioUnitario, d.Descuento, d.PrecioTotalSinImpuesto,
                        (CodigoIva)d.CodigoIva, d.IvaBase, d.IvaValor, d.IceValor));

                return Factura.Reconstituir(
                    fila.Id, fila.EmpresaRuc, fila.ClaveAcceso, (Ambiente)fila.Ambiente,
                    fila.Estab, fila.PtoEmi, fila.Secuencial, fila.FechaEmision,
                    EstadoSriMapper.DesdeBaseDatos(fila.EstadoSri),
                    fila.RazonSocialComprador, fila.IdentificacionComprador,
                    (TipoIdentificacion)fila.TipoIdentificacionComprador, fila.DireccionComprador,
                    fila.GuiaRemision, fila.TotalSinImpuestos, fila.TotalDescuento, fila.TotalIva,
                    fila.TotalIce, fila.ImporteTotal, fila.Moneda, fila.NumeroAutorizacion,
                    fila.FechaAutorizacion, fila.SriRespuesta, fila.XmlFirmadoPath,
                    fila.XmlAutorizadoPath, fila.PdfPath, fila.CreatedAt, fila.UpdatedAt,
                    detalles,
                    JsonColumnas.Deserializar<FormaPago>(fila.FormasPago),
                    JsonColumnas.Deserializar<InfoAdicional>(fila.InfoAdicional));
            }
        }

        public async Task<bool> ExisteSecuencialActivoAsync(string empresaRuc, string estab, string ptoEmi, string secuencial, Ambiente ambiente)
        {
            // El secuencial está "activo" si bloquea: cualquier estado salvo PENDIENTE y NO_AUTORIZADO.
            const string sql = @"
                SELECT CASE WHEN EXISTS (
                    SELECT 1 FROM dbo.facturas
                    WHERE empresa_ruc = @empresaRuc AND estab = @estab AND pto_emi = @ptoEmi
                      AND secuencial = @secuencial AND ambiente = @ambiente
                      AND estado_sri <> 'PENDIENTE' AND estado_sri <> 'NO_AUTORIZADO'
                ) THEN 1 ELSE 0 END;";

            using (var con = _fabrica.Crear())
                return await con.ExecuteScalarAsync<bool>(sql, new
                {
                    empresaRuc,
                    estab,
                    ptoEmi,
                    secuencial,
                    ambiente = (int)ambiente
                });
        }

        // ─── Escritura ──────────────────────────────────────────────────────────

        public async Task AgregarAsync(Factura factura)
        {
            const string sqlFactura = @"
                INSERT INTO dbo.facturas
                    (empresa_ruc, clave_acceso, ambiente, estab, pto_emi, secuencial,
                     fecha_emision, estado_sri, tipo_identificacion_comprador,
                     identificacion_comprador, razon_social_comprador, direccion_comprador,
                     guia_remision, total_sin_impuestos, total_descuento, total_iva, total_ice,
                     importe_total, moneda, formas_pago, info_adicional, numero_autorizacion,
                     fecha_autorizacion, sri_respuesta, xml_firmado_path, xml_autorizado_path,
                     pdf_path, created_at, updated_at)
                OUTPUT INSERTED.id
                VALUES
                    (@EmpresaRuc, @ClaveAcceso, @Ambiente, @Estab, @PtoEmi, @Secuencial,
                     @FechaEmision, @EstadoSri, @TipoIdentificacionComprador,
                     @IdentificacionComprador, @RazonSocialComprador, @DireccionComprador,
                     @GuiaRemision, @TotalSinImpuestos, @TotalDescuento, @TotalIva, @TotalIce,
                     @ImporteTotal, @Moneda, @FormasPago, @InfoAdicional, @NumeroAutorizacion,
                     @FechaAutorizacion, @SriRespuesta, @XmlFirmadoPath, @XmlAutorizadoPath,
                     @PdfPath, @CreatedAt, @UpdatedAt);";

            const string sqlDetalle = @"
                INSERT INTO dbo.facturas_detalle
                    (factura_id, codigo_principal, codigo_auxiliar, descripcion, cantidad,
                     precio_unitario, descuento, precio_total_sin_impuesto, codigo_iva,
                     iva_base, iva_valor, ice_valor)
                VALUES
                    (@FacturaId, @CodigoPrincipal, @CodigoAuxiliar, @Descripcion, @Cantidad,
                     @PrecioUnitario, @Descuento, @PrecioTotalSinImpuesto, @CodigoIva,
                     @IvaBase, @IvaValor, @IceValor);";

            using (var con = (DbConnection)_fabrica.Crear())
            {
                await con.OpenAsync();
                using (var tx = con.BeginTransaction())
                {
                    var id = await con.ExecuteScalarAsync<int>(sqlFactura, new
                    {
                        factura.EmpresaRuc,
                        factura.ClaveAcceso,
                        Ambiente = (int)factura.Ambiente,
                        factura.Estab,
                        factura.PtoEmi,
                        factura.Secuencial,
                        factura.FechaEmision,
                        EstadoSri = EstadoSriMapper.ABaseDatos(factura.EstadoSri),
                        TipoIdentificacionComprador = (int)factura.TipoIdentificacionComprador,
                        factura.IdentificacionComprador,
                        factura.RazonSocialComprador,
                        factura.DireccionComprador,
                        factura.GuiaRemision,
                        factura.TotalSinImpuestos,
                        factura.TotalDescuento,
                        factura.TotalIva,
                        factura.TotalIce,
                        factura.ImporteTotal,
                        factura.Moneda,
                        FormasPago = JsonColumnas.Serializar(factura.FormasPago),
                        InfoAdicional = JsonColumnas.Serializar(factura.InfoAdicional),
                        factura.NumeroAutorizacion,
                        factura.FechaAutorizacion,
                        factura.SriRespuesta,
                        factura.XmlFirmadoPath,
                        factura.XmlAutorizadoPath,
                        factura.PdfPath,
                        factura.CreatedAt,
                        factura.UpdatedAt
                    }, tx);

                    factura.AsignarId(id);

                    foreach (var d in factura.Detalles)
                    {
                        await con.ExecuteAsync(sqlDetalle, new
                        {
                            FacturaId = id,
                            d.CodigoPrincipal,
                            d.CodigoAuxiliar,
                            d.Descripcion,
                            d.Cantidad,
                            d.PrecioUnitario,
                            d.Descuento,
                            d.PrecioTotalSinImpuesto,
                            CodigoIva = (int)d.CodigoIva,
                            d.IvaBase,
                            d.IvaValor,
                            d.IceValor
                        }, tx);
                    }

                    tx.Commit();
                }
            }
        }

        // Solo actualiza la cabecera — los checkpoints del orquestador nunca tocan el detalle.
        public async Task ActualizarAsync(Factura factura)
        {
            const string sql = @"
                UPDATE dbo.facturas SET
                    estado_sri          = @EstadoSri,
                    numero_autorizacion = @NumeroAutorizacion,
                    fecha_autorizacion  = @FechaAutorizacion,
                    sri_respuesta       = @SriRespuesta,
                    xml_firmado_path    = @XmlFirmadoPath,
                    xml_autorizado_path = @XmlAutorizadoPath,
                    pdf_path            = @PdfPath,
                    updated_at          = @UpdatedAt
                WHERE id = @Id;";

            using (var con = _fabrica.Crear())
                await con.ExecuteAsync(sql, new
                {
                    EstadoSri = EstadoSriMapper.ABaseDatos(factura.EstadoSri),
                    factura.NumeroAutorizacion,
                    factura.FechaAutorizacion,
                    factura.SriRespuesta,
                    factura.XmlFirmadoPath,
                    factura.XmlAutorizadoPath,
                    factura.PdfPath,
                    factura.UpdatedAt,
                    factura.Id
                });
        }

        // ─── DTOs de lectura (mapeo Dapper snake_case → PascalCase) ───────────────

        private class FilaFactura
        {
            public int Id { get; set; }
            public string EmpresaRuc { get; set; }
            public string ClaveAcceso { get; set; }
            public int Ambiente { get; set; }
            public string Estab { get; set; }
            public string PtoEmi { get; set; }
            public string Secuencial { get; set; }
            public DateTime FechaEmision { get; set; }
            public string EstadoSri { get; set; }
            public int TipoIdentificacionComprador { get; set; }
            public string IdentificacionComprador { get; set; }
            public string RazonSocialComprador { get; set; }
            public string DireccionComprador { get; set; }
            public string GuiaRemision { get; set; }
            public decimal TotalSinImpuestos { get; set; }
            public decimal TotalDescuento { get; set; }
            public decimal TotalIva { get; set; }
            public decimal TotalIce { get; set; }
            public decimal ImporteTotal { get; set; }
            public string Moneda { get; set; }
            public string FormasPago { get; set; }
            public string InfoAdicional { get; set; }
            public string NumeroAutorizacion { get; set; }
            public DateTimeOffset? FechaAutorizacion { get; set; }
            public string SriRespuesta { get; set; }
            public string XmlFirmadoPath { get; set; }
            public string XmlAutorizadoPath { get; set; }
            public string PdfPath { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime UpdatedAt { get; set; }
        }

        private class FilaDetalle
        {
            public int Id { get; set; }
            public int FacturaId { get; set; }
            public string CodigoPrincipal { get; set; }
            public string CodigoAuxiliar { get; set; }
            public string Descripcion { get; set; }
            public decimal Cantidad { get; set; }
            public decimal PrecioUnitario { get; set; }
            public decimal Descuento { get; set; }
            public decimal PrecioTotalSinImpuesto { get; set; }
            public int CodigoIva { get; set; }
            public decimal IvaBase { get; set; }
            public decimal IvaValor { get; set; }
            public decimal IceValor { get; set; }
        }
    }
}
