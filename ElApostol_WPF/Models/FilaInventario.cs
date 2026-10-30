namespace ElApostol_WPF.Models
{
    /// <summary>Una fila del inventario: un producto con una de sus variantes (o sin variante).</summary>
    public class FilaInventario
    {
        public int ProductoId { get; set; }
        public int? VarianteId { get; set; }
        public string CodigoInterno { get; set; } = "";
        public string Nombre { get; set; } = "";
        public string? Marca { get; set; }
        public string? NombreCategoria { get; set; }
        public bool Activo { get; set; }
        public string? Medida { get; set; }
        public decimal PrecioUnitario { get; set; }
        public int StockActual { get; set; }
        public int StockMinimo { get; set; }

        /// <summary>"crit" (sin stock o menos de la mitad del mínimo), "low" (bajo el mínimo) u "ok".</summary>
        public string StockEstado =>
            VarianteId == null ? "none"
            : StockActual <= 0 || StockActual < StockMinimo / 2.0 ? "crit"
            : StockActual < StockMinimo ? "low"
            : "ok";
    }
}
