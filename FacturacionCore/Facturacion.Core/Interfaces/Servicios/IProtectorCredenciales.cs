namespace Facturacion.Core.Interfaces.Servicios
{
    // Cifrado/descifrado de credenciales sensibles en reposo (ej. password del .p12).
    public interface IProtectorCredenciales
    {
        string Cifrar(string textoPlano);
        string Descifrar(string textoCifrado);
    }
}
