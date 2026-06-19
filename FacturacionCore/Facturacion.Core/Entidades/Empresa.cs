using System;

namespace Facturacion.Core.Entidades
{
    public class Empresa
    {
        public int Id { get; private set; }
        public string Ruc { get; private set; }
        public string RazonSocial { get; private set; }
        public string NombreComercial { get; private set; }
        public string DirMatriz { get; private set; }
        public bool ObligadoContabilidad { get; private set; }
        public string CertificadoPath { get; private set; }
        public string CertPassword { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime UpdatedAt { get; private set; }

        private Empresa() { }

        public void AsignarId(int id) => Id = id;

        // Usado por la capa de persistencia para inyectar el password ya descifrado tras leer de BD.
        public void AsignarCertPassword(string password) => CertPassword = password;

        public static Empresa Crear(
            string ruc,
            string razonSocial,
            string nombreComercial,
            string dirMatriz,
            bool obligadoContabilidad,
            string certificadoPath,
            string certPassword)
        {
            return new Empresa
            {
                Ruc = ruc,
                RazonSocial = razonSocial,
                NombreComercial = nombreComercial,
                DirMatriz = dirMatriz,
                ObligadoContabilidad = obligadoContabilidad,
                CertificadoPath = certificadoPath,
                CertPassword = certPassword,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }

        public void ActualizarCertificado(string path, string password)
        {
            CertificadoPath = path;
            CertPassword = password;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
