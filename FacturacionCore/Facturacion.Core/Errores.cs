using ErrorOr;

namespace Facturacion.Core
{
    public static class Errores
    {
        public static class Empresa
        {
            public static Error NoEncontrada => Error.NotFound("Empresa.NoEncontrada", "La empresa no existe.");
            public static Error RucDuplicado => Error.Conflict("Empresa.RucDuplicado", "Ya existe una empresa con ese RUC.");
        }

        public static class Factura
        {
            public static Error NoEncontrada => Error.NotFound("Factura.NoEncontrada", "La factura no existe.");
            public static Error SecuencialDuplicado => Error.Conflict("Factura.SecuencialDuplicado", "Ya existe un comprobante activo con ese secuencial.");
        }

        public static class Sri
        {
            public static Error NoAutorizado(string detalle = null) =>
                Error.Failure("Sri.NoAutorizado", detalle ?? "El SRI rechazó el comprobante.");
            public static Error SecuencialDuplicado =>
                Error.Conflict("Sri.SecuencialDuplicado", "Clave de acceso ya registrada en el SRI.");
            public static Error EnProcesamiento =>
                Error.Failure("Sri.EnProcesamiento", "El SRI continúa en procesamiento tras los reintentos.");
            public static Error Devuelta(string detalle = null) =>
                Error.Failure("Sri.Devuelta", detalle ?? "El SRI devolvió el comprobante.");
            public static Error ErrorComunicacion =>
                Error.Failure("Sri.ErrorComunicacion", "Error de comunicación con el SRI.");
            public static Error SinRespuesta =>
                Error.Failure("Sri.SinRespuesta", "El SRI no devolvió una respuesta reconocible.");
        }

        public static class Firma
        {
            public static Error ErrorFirma =>
                Error.Failure("Firma.ErrorFirma", "Error al firmar el XML.");
            public static Error CertificadoInvalido =>
                Error.Failure("Firma.CertificadoInvalido", "El certificado P12 es inválido o la contraseña es incorrecta.");
        }

        public static class Storage
        {
            public static Error ErrorGuardar =>
                Error.Failure("Storage.ErrorGuardar", "Error al guardar el archivo.");
            public static Error ErrorObtener =>
                Error.Failure("Storage.ErrorObtener", "Error al obtener el archivo.");
            public static Error NoEncontrado =>
                Error.NotFound("Storage.NoEncontrado", "El archivo no existe en el almacenamiento.");
        }

        public static class Xml
        {
            public static Error ErrorGeneracion =>
                Error.Failure("Xml.ErrorGeneracion", "Error al generar el XML del comprobante.");
        }

        public static class Pdf
        {
            public static Error ErrorGeneracion =>
                Error.Failure("Pdf.ErrorGeneracion", "Error al generar el PDF del RIDE.");
        }

        public static class Secuencial
        {
            public static Error NoConfigurado =>
                Error.NotFound("Secuencial.NoConfigurado", "No existe un secuencial configurado para este establecimiento y punto de emisión.");
        }

        public static class Parametros
        {
            public static Error NoEncontrados =>
                Error.NotFound("Parametros.NoEncontrados", "No existen parámetros de facturación para esta empresa.");
        }
    }
}
