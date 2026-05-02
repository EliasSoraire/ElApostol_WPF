namespace ElApostol_WPF.Models
{
    public class ResumenTurno
    {
        public int CantidadVentas { get; set; }
        public decimal TotalRecaudado { get; set; }
        public Venta? UltimaVenta { get; set; }
        public string? UltimaVentaDesc { get; set; }
    }
}