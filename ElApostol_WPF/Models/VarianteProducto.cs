namespace ElApostol_WPF.Models
{
    public class VarianteProducto
    {
        public int Id { get; set; }
        public int ProductoId { get; set; }
        public string? Medida { get; set; }
        public string? Calidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public int StockActual { get; set; }
        public int StockMinimo { get; set; }
    }
}