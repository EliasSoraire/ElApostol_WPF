using Microsoft.Data.SqlClient;
using ElApostol_WPF.Models;

namespace ElApostol_WPF.Data
{
    public class UsuarioRepository
    {
        public Usuario? ObtenerPorNombreUsuario(string nombreUsuario)
        {
            using var conn = ConexionDB.ObtenerConexion();
            conn.Open();

            var cmd = new SqlCommand(@"
                SELECT id, nombre_completo, usuario, contrasena_hash, rol,
                       activo, fecha_alta, ultimo_login, hora_inicio_sesion,
                       contrasena_es_temporal, pregunta_secreta, respuesta_secreta_hash
                FROM usuarios
                WHERE usuario = @usuario AND activo = 1", conn);

            cmd.Parameters.AddWithValue("@usuario", nombreUsuario);

            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return null;

            return MapearUsuario(reader);
        }

        public Usuario? ObtenerPorId(int id)
        {
            using var conn = ConexionDB.ObtenerConexion();
            conn.Open();

            var cmd = new SqlCommand(@"
                SELECT id, nombre_completo, usuario, contrasena_hash, rol,
                       activo, fecha_alta, ultimo_login, hora_inicio_sesion,
                       contrasena_es_temporal, pregunta_secreta, respuesta_secreta_hash
                FROM usuarios
                WHERE id = @id", conn);

            cmd.Parameters.AddWithValue("@id", id);

            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return null;

            return MapearUsuario(reader);
        }

        public void ActualizarUltimoLogin(int usuarioId)
        {
            using var conn = ConexionDB.ObtenerConexion();
            conn.Open();

            var cmd = new SqlCommand(@"
                UPDATE usuarios
                SET ultimo_login = GETDATE(), hora_inicio_sesion = GETDATE()
                WHERE id = @id", conn);

            cmd.Parameters.AddWithValue("@id", usuarioId);
            cmd.ExecuteNonQuery();
        }

        public void ActualizarContrasena(int usuarioId, string nuevaContrasenaHash, bool esTemporal = false)
        {
            using var conn = ConexionDB.ObtenerConexion();
            conn.Open();

            var cmd = new SqlCommand(@"
                UPDATE usuarios
                SET contrasena_hash = @hash, contrasena_es_temporal = @temporal
                WHERE id = @id", conn);

            cmd.Parameters.AddWithValue("@hash", nuevaContrasenaHash);
            cmd.Parameters.AddWithValue("@temporal", esTemporal ? 1 : 0);
            cmd.Parameters.AddWithValue("@id", usuarioId);
            cmd.ExecuteNonQuery();
        }

        private static Usuario MapearUsuario(SqlDataReader r) => new()
        {
            Id = r.GetInt32(r.GetOrdinal("id")),
            NombreCompleto = r.GetString(r.GetOrdinal("nombre_completo")),
            NombreUsuario = r.GetString(r.GetOrdinal("usuario")),
            ContrasenaHash = r.GetString(r.GetOrdinal("contrasena_hash")),
            Rol = r.GetString(r.GetOrdinal("rol")),
            Activo = r.GetBoolean(r.GetOrdinal("activo")),
            FechaAlta = r.GetDateTime(r.GetOrdinal("fecha_alta")),
            UltimoLogin = r.IsDBNull(r.GetOrdinal("ultimo_login")) ? null : r.GetDateTime(r.GetOrdinal("ultimo_login")),
            HoraInicioSesion = r.IsDBNull(r.GetOrdinal("hora_inicio_sesion")) ? null : r.GetDateTime(r.GetOrdinal("hora_inicio_sesion")),
            ContrasenaEsTemporal = r.GetBoolean(r.GetOrdinal("contrasena_es_temporal")),
            PreguntaSecreta = r.IsDBNull(r.GetOrdinal("pregunta_secreta")) ? null : r.GetString(r.GetOrdinal("pregunta_secreta")),
            RespuestaSecretaHash = r.IsDBNull(r.GetOrdinal("respuesta_secreta_hash")) ? null : r.GetString(r.GetOrdinal("respuesta_secreta_hash")),
        };

        public List<Usuario> ObtenerEmpleados()
        {
            using var conn = ConexionDB.ObtenerConexion();
            conn.Open();
            var cmd = new SqlCommand(@"
        SELECT id, nombre_completo, usuario, contrasena_hash, rol,
               activo, fecha_alta, ultimo_login, hora_inicio_sesion,
               contrasena_es_temporal, pregunta_secreta, respuesta_secreta_hash
        FROM usuarios
        WHERE rol = 'empleado'
        ORDER BY nombre_completo", conn);

            var lista = new List<Usuario>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                lista.Add(MapearUsuario(reader));
            return lista;
        }

        public void CambiarEstadoEmpleado(int usuarioId, bool activo)
        {
            using var conn = ConexionDB.ObtenerConexion();
            conn.Open();
            var cmd = new SqlCommand(@"
        UPDATE usuarios SET activo = @activo WHERE id = @id", conn);
            cmd.Parameters.AddWithValue("@activo", activo ? 1 : 0);
            cmd.Parameters.AddWithValue("@id", usuarioId);
            cmd.ExecuteNonQuery();
        }

        public void CrearEmpleado(string nombreCompleto, string nombreUsuario, string contrasenaHash)
        {
            using var conn = ConexionDB.ObtenerConexion();
            conn.Open();
            var cmd = new SqlCommand(@"
        INSERT INTO usuarios (nombre_completo, usuario, contrasena_hash, rol, activo, contrasena_es_temporal)
        VALUES (@nombre, @usuario, @hash, 'empleado', 1, 1)", conn);
            cmd.Parameters.AddWithValue("@nombre", nombreCompleto);
            cmd.Parameters.AddWithValue("@usuario", nombreUsuario);
            cmd.Parameters.AddWithValue("@hash", contrasenaHash);
            cmd.ExecuteNonQuery();
        }

        public void ActualizarPreguntaSecreta(int usuarioId, string pregunta, string respuestaHash)
        {
            using var conn = ConexionDB.ObtenerConexion();
            conn.Open();

            var cmd = new SqlCommand(@"
        UPDATE usuarios
        SET pregunta_secreta = @pregunta,
            respuesta_secreta_hash = @respHash
        WHERE id = @id", conn);

            cmd.Parameters.AddWithValue("@pregunta", pregunta);
            cmd.Parameters.AddWithValue("@respHash", respuestaHash);
            cmd.Parameters.AddWithValue("@id", usuarioId);
            cmd.ExecuteNonQuery();
        }
    }
}