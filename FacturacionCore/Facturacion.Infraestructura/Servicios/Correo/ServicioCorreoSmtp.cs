using System;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using ErrorOr;
using Facturacion.Core;
using Facturacion.Core.Interfaces.Servicios;
using Microsoft.Extensions.Logging;

namespace Facturacion.Infraestructura.Servicios.Correo
{
    // Envío de correo vía SMTP (System.Net.Mail). Falla cerrada: si no hay Host/Remitente
    // configurados devuelve Correo.NoConfigurado en vez de lanzar.
    public class ServicioCorreoSmtp : IServicioCorreo
    {
        private readonly ConfiguracionSmtp _config;
        private readonly ILogger<ServicioCorreoSmtp> _logger;

        public ServicioCorreoSmtp(ConfiguracionSmtp config, ILogger<ServicioCorreoSmtp> logger)
        {
            _config = config;
            _logger = logger;
        }

        public async Task<ErrorOr<Success>> EnviarAsync(MensajeCorreo mensaje)
        {
            if (_config == null || string.IsNullOrWhiteSpace(_config.Host) || string.IsNullOrWhiteSpace(_config.Remitente))
                return Errores.Correo.NoConfigurado;

            var destinatarios = mensaje?.Para?.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
            if (destinatarios == null || destinatarios.Count == 0)
                return Errores.Correo.DestinatarioInvalido;

            try
            {
                using (var mail = new MailMessage())
                {
                    mail.From = new MailAddress(_config.Remitente,
                        string.IsNullOrWhiteSpace(_config.NombreRemitente) ? _config.Remitente : _config.NombreRemitente);
                    foreach (var destino in destinatarios)
                        mail.To.Add(destino);

                    mail.Subject = mensaje.Asunto ?? string.Empty;
                    mail.Body = mensaje.CuerpoHtml ?? string.Empty;
                    mail.IsBodyHtml = true;

                    if (mensaje.Adjuntos != null)
                    {
                        foreach (var adjunto in mensaje.Adjuntos)
                        {
                            if (adjunto?.Contenido == null || adjunto.Contenido.Length == 0) continue;
                            var stream = new System.IO.MemoryStream(adjunto.Contenido);
                            var tipo = string.IsNullOrWhiteSpace(adjunto.TipoContenido)
                                ? "application/octet-stream"
                                : adjunto.TipoContenido;
                            // El Attachment (y su stream) se libera al hacer Dispose del MailMessage.
                            mail.Attachments.Add(new Attachment(stream, adjunto.NombreArchivo, tipo));
                        }
                    }

                    using (var cliente = new SmtpClient(_config.Host, _config.Puerto))
                    {
                        cliente.EnableSsl = _config.UsarSsl;
                        if (!string.IsNullOrWhiteSpace(_config.Usuario))
                            cliente.Credentials = new NetworkCredential(_config.Usuario, _config.Password);

                        await cliente.SendMailAsync(mail);
                    }
                }

                return Result.Success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enviando correo a {Destinatarios}", string.Join(", ", destinatarios));
                return Errores.Correo.ErrorEnvio;
            }
        }
    }
}
