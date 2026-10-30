using BCrypt.Net;
using ElApostol_WPF.Data;
using ElApostol_WPF.Models;

namespace ElApostol_WPF.Business
{
    public class AuthService
    {
        private readonly UsuarioRepository _repo = new();

        public Usuario? Login(string nombreUsuario, string contrasena)
        {
            var usuario = _repo.ObtenerPorNombreUsuario(nombreUsuario);
            if (usuario == null) return null;

            if (!BCrypt.Net.BCrypt.Verify(contrasena, usuario.ContrasenaHash))
                return null;

            _repo.ActualizarUltimoLogin(usuario.Id);

            // Releer para obtener la hora_inicio_sesion recién guardada (la anterior es de la sesión previa)
            return _repo.ObtenerPorId(usuario.Id) ?? usuario;
        }

        public bool CambiarContrasena(int usuarioId, string nuevaContrasena, bool esTemporal = false)
        {
            try
            {
                var hash = BCrypt.Net.BCrypt.HashPassword(nuevaContrasena);
                _repo.ActualizarContrasena(usuarioId, hash, esTemporal);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool VerificarContrasenaActual(int usuarioId, string contrasena)
        {
            var usuario = _repo.ObtenerPorId(usuarioId);
            if (usuario == null) return false;

            return BCrypt.Net.BCrypt.Verify(contrasena, usuario.ContrasenaHash);
        }

        public bool GuardarPreguntaSecreta(int usuarioId, string pregunta, string respuesta)
        {
            try
            {
                var hash = BCrypt.Net.BCrypt.HashPassword(respuesta.Trim().ToLower());
                _repo.ActualizarPreguntaSecreta(usuarioId, pregunta, hash);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool VerificarRespuestaSecreta(int usuarioId, string respuesta)
        {
            var usuario = _repo.ObtenerPorId(usuarioId);
            if (usuario == null || string.IsNullOrEmpty(usuario.RespuestaSecretaHash))
                return false;
            return BCrypt.Net.BCrypt.Verify(respuesta.Trim().ToLower(), usuario.RespuestaSecretaHash);
        }

    }
}