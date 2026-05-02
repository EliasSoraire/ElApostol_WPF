namespace ElApostol_WPF.Models
{
    public class Usuario
    {
        public int Id { get; set; }
        public string NombreCompleto { get; set; }
        public string NombreUsuario { get; set; }
        public string ContrasenaHash { get; set; }
        public string Rol { get; set; } 
        public bool Activo { get; set; }
        public DateTime FechaAlta { get; set; }
        public DateTime? UltimoLogin { get; set; }
        public DateTime? HoraInicioSesion { get; set; }
        public bool ContrasenaEsTemporal { get; set; }
        public string? PreguntaSecreta { get; set; }
        public string? RespuestaSecretaHash { get; set; }
    }
}