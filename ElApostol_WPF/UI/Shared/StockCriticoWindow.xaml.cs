using ElApostol_WPF.Data;
using ElApostol_WPF.Models;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace ElApostol_WPF.UI.Shared
{
    public partial class StockCriticoWindow : Window
    {
        public StockCriticoWindow()
        {
            InitializeComponent();
            Cargar();
        }

        private void Cargar()
        {
            try
            {
                var criticos = new ProductoRepository().ObtenerVariantesCriticas();
                var sinStock = criticos.Count(c => c.StockActual <= 0);

                ListaCriticos.ItemsSource = criticos.Select(c => new StockCriticoRow(c)).ToList();
                TxtSinStock.Text = sinStock.ToString();
                TxtBajoMinimo.Text = (criticos.Count - sinStock).ToString();
                TxtSubtitulo.Text = criticos.Count == 1
                    ? "1 variante necesita reposición"
                    : $"{criticos.Count} variantes necesitan reposición";
                TxtVacio.Visibility = criticos.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                Dialogo.Show($"No se pudo cargar el stock crítico:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCerrar_Click(object sender, RoutedEventArgs e) => Close();

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) Close();
        }
    }

    public class StockCriticoRow
    {
        public string CodigoInterno { get; }
        public string Nombre { get; }
        public string Detalle { get; }
        public string Medida { get; }
        public int StockActual { get; }
        public int StockMinimo { get; }
        public string Faltante { get; }
        public Brush BadgeBg { get; }
        public Brush BadgeFg { get; }

        public StockCriticoRow(FilaInventario f)
        {
            CodigoInterno = f.CodigoInterno;
            Nombre = f.Nombre;
            Detalle = string.Join(" · ", new[] { f.Marca, f.NombreCategoria }.Where(s => !string.IsNullOrWhiteSpace(s)));
            Medida = f.Medida ?? "-";
            StockActual = f.StockActual;
            StockMinimo = f.StockMinimo;
            Faltante = $"+{Math.Max(0, f.StockMinimo - f.StockActual)}";

            var sinStock = f.StockActual <= 0;
            BadgeBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString(sinStock ? "#FCEBEB" : "#FAEEDA"));
            BadgeFg = new SolidColorBrush((Color)ColorConverter.ConvertFromString(sinStock ? "#791F1F" : "#633806"));
        }
    }
}
