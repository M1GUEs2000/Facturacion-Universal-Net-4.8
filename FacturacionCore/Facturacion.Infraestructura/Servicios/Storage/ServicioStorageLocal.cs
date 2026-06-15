using System;
using System.IO;
using System.Threading.Tasks;
using ErrorOr;
using Facturacion.Core;
using Facturacion.Core.Interfaces.Servicios;
using Microsoft.Extensions.Logging;

namespace Facturacion.Infraestructura.Servicios.Storage
{
    // Almacenamiento en filesystem local. Las rutas lógicas usan '/' (p.ej.
    // "XMLFIRMADOS/{ruc}/{clave}.xml") y se resuelven contra _basePath.
    public class ServicioStorageLocal : IServicioStorage
    {
        private readonly string _basePath;
        private readonly ILogger<ServicioStorageLocal> _logger;

        public ServicioStorageLocal(string basePath, ILogger<ServicioStorageLocal> logger)
        {
            _basePath = basePath ?? throw new ArgumentNullException(nameof(basePath));
            _logger = logger;
        }

        public Task<ErrorOr<string>> GuardarAsync(string ruta, byte[] contenido)
        {
            try
            {
                var fullPath = ResolverRuta(ruta);
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                File.WriteAllBytes(fullPath, contenido);
                return Task.FromResult<ErrorOr<string>>(ruta);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error guardando archivo en {Ruta}", ruta);
                return Task.FromResult<ErrorOr<string>>(Errores.Storage.ErrorGuardar);
            }
        }

        public Task<ErrorOr<byte[]>> ObtenerAsync(string ruta)
        {
            try
            {
                var fullPath = ResolverRuta(ruta);
                if (!File.Exists(fullPath))
                    return Task.FromResult<ErrorOr<byte[]>>(Errores.Storage.NoEncontrado);

                return Task.FromResult<ErrorOr<byte[]>>(File.ReadAllBytes(fullPath));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo archivo de {Ruta}", ruta);
                return Task.FromResult<ErrorOr<byte[]>>(Errores.Storage.ErrorObtener);
            }
        }

        // Eliminación idempotente: si el archivo ya no existe se considera éxito.
        public Task<ErrorOr<bool>> EliminarAsync(string ruta)
        {
            try
            {
                var fullPath = ResolverRuta(ruta);
                if (File.Exists(fullPath))
                    File.Delete(fullPath);
                return Task.FromResult<ErrorOr<bool>>(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error eliminando archivo de {Ruta}", ruta);
                return Task.FromResult<ErrorOr<bool>>(Errores.Storage.ErrorGuardar);
            }
        }

        private string ResolverRuta(string ruta)
        {
            var relativa = ruta.Replace('/', Path.DirectorySeparatorChar);
            return Path.GetFullPath(Path.Combine(_basePath, relativa));
        }
    }
}
