namespace ElApostol_WPF.Models
{
    /// <summary>Venta con su vendedor y sus renglones, para ver el detalle o exportarlo.</summary>
    public class VentaDetallada
    {
        public int Id { get; set; }
        public string NumeroComprobante { get; set; } = "";
        public DateTime FechaHora { get; set; }
        public int UsuarioId { get; set; }
        public string Empleado { get; set; } = "";
        public string MetodoPago { get; set; } = "";
        public decimal Subtotal { get; set; }
        public decimal MontoRecargo { get; set; }
        public decimal Total { get; set; }
        public string? Observaciones { get; set; }
        public List<DetalleVenta> Lineas { get; set; } = new();

        public int Unidades => Lineas.Sum(l => l.Cantidad);

        public string MetodoPagoDisplay => MetodoPago switch
        {
            "efectivo" => "Efectivo",
            "tarjeta" => "Tarjeta",
            "transferencia" => "Transferencia",
            _ => MetodoPago
        };
    }
}
