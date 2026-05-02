namespace ElApostol_WPF.Models
{
    public class Producto
    {
        public int Id { get; set; }
        public string CodigoInterno { get; set; }
        public string Nombre { get; set; }
        public string? Marca { get; set; }
        public int CategoriaId { get; set; }
        public string? NombreCategoria { get; set; } 
        public string? Proveedor { get; set; }
        public List<VarianteProducto> Variantes { get; set; } = new();
    }
}