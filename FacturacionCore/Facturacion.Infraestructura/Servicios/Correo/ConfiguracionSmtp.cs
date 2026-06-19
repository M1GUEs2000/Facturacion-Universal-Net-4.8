namespace Facturacion.Infraestructura.Servicios.Correo
{
    // Configuración del servidor SMTP. La arma el CompositionRoot desde appSettings
    // (secrets.config / Web.config) y se inyecta a ServicioCorreoSmtp.
    public sealed class ConfiguracionSmtp
    {
        public string Host { get; set; }
        public int Puerto { get; set; } = 587;
        public bool UsarSsl { get; set; } = true;
        public string Usuario { get; set; }
        public string Password { get; set; }
        public string Remitente { get; set; }         // dirección "From"
        public string NombreRemitente { get; set; }    // nombre visible del remitente
    }
}
