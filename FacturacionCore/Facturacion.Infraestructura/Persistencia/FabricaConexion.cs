using System;
using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Facturacion.Infraestructura.Persistencia
{
    public class FabricaConexion : IFabricaConexion
    {
        private readonly string _cadenaConexion;

        static FabricaConexion()
        {
            // Las columnas usan snake_case; las propiedades de las entidades PascalCase.
            // Esto hace que empresa_ruc -> EmpresaRuc, etc. se mapeen automáticamente.
            DefaultTypeMap.MatchNamesWithUnderscores = true;
        }

        public FabricaConexion(string cadenaConexion)
        {
            _cadenaConexion = cadenaConexion ?? throw new ArgumentNullException(nameof(cadenaConexion));
        }

        public IDbConnection Crear() => new SqlConnection(_cadenaConexion);
    }
}
