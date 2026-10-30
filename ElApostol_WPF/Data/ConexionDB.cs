using Microsoft.Data.SqlClient;

namespace ElApostol_WPF.Data
{
    public class ConexionDB
    {
        private static readonly string _connectionString =
            @"Server=(localdb)\MSSQLLocalDB;Database=ElApostol;Trusted_Connection=True;TrustServerCertificate=True;";

        public static SqlConnection ObtenerConexion()
        {
            return new SqlConnection(_connectionString);
        }

        public static bool ProbarConexion()
        {
            try
            {
                using var conn = ObtenerConexion();
                conn.Open();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}