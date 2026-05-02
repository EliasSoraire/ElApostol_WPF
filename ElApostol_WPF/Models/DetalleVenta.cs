namespace ElApostol_WPF.Models
{
    public class DetalleVenta
    {
        public int Id { get; set; }
        public int VentaId { get; set; }
        public int VarianteId { get; set; }
        public string? NombreProducto { get; set; } 
        public string? Medida { get; set; }          
        public int Cantidad { get; set; }
        public decimal PrecioUnitarioHistorico { get; set; }
        public decimal SubtotalLinea { get; set; }
    }
}