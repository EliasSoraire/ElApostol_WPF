namespace ElApostol_WPF.Models
{
    public class Venta
    {
        public int Id { get; set; }
        public string NumeroComprobante { get; set; }
        public DateTime FechaHora { get; set; }
        public int UsuarioId { get; set; }
        public string MetodoPago { get; set; } 
        public decimal Subtotal { get; set; }
        public decimal MontoRecargo { get; set; }
        public decimal Total { get; set; }
        public string? Observaciones { get; set; }
        public List<DetalleVenta> Detalles { get; set; } = new();
    }
}