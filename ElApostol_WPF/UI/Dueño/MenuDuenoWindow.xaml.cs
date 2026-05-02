using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ElApostol_WPF.Data;
using ElApostol_WPF.Models;
using ElApostol_WPF.UI.Login;

namespace ElApostol_WPF.UI.Dueño
{
    public partial class MenuDuenoWindow : Window
    {
        private readonly Usuario _usuario;
        private readonly ProductoRepository _repo = new();

        private int _paginaActual = 1;
        private const int PorPagina = 10;
        private int _totalRegistros = 0;
        private bool _placeholderActivo = true;

        public MenuDuenoWindow(Usuario usuario)
        {
            InitializeComponent();
            _usuario = usuario;

            TxtNombreUsuario.Text = usuario.NombreCompleto;
            TxtAvatar.Text = ObtenerIniciales(usuario.NombreCompleto);
            TxtFecha.Text = DateTime.Now.ToString("dddd d 'de' MMMM, yyyy",
                                        new System.Globalization.CultureInfo("es-AR"));

            TxtBusqueda.Text = TxtBusqueda.Tag?.ToString();
            TxtBusqueda.Foreground = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString("#AAAAAA"));

            CargarCategorias();
            CargarMetricas();
            CargarProductos();
        }

        private void CargarMetricas()
        {
            var (total, criticos, valor) = _repo.ObtenerMetricas();
            var ventasHoy = _repo.ObtenerVentasHoy();

            TxtTotalProductos.Text = total.ToString();
            TxtStockCritico.Text = criticos.ToString();
            TxtValorInventario.Text = $"${valor:N0}";
            TxtVentasHoy.Text = ventasHoy.ToString();
        }

        private void CargarCategorias()
        {
            var cats = _repo.ObtenerCategorias();
            CmbCategoria.Items.Clear();
            CmbCategoria.Items.Add(new ComboBoxItem { Content = "Todas las categorías", Tag = null });
            foreach (var c in cats)
                CmbCategoria.Items.Add(new ComboBoxItem { Content = c.Nombre, Tag = c.Id });
            CmbCategoria.SelectedIndex = 0;
        }

        private void CargarProductos()
        {
            var busqueda = _placeholderActivo ? "" : TxtBusqueda.Text.Trim();
            var catItem = CmbCategoria.SelectedItem as ComboBoxItem;
            var categoriaId = catItem?.Tag as int?;

            var productos = _repo.ObtenerPaginado(
                busqueda, categoriaId, _paginaActual, PorPagina, out _totalRegistros);

            DgProductos.ItemsSource = productos.Select(p => new ProductoRow(p)).ToList();

            var desde = (_paginaActual - 1) * PorPagina + 1;
            var hasta = Math.Min(_paginaActual * PorPagina, _totalRegistros);
            TxtPaginaInfo.Text = _totalRegistros == 0
                ? "Sin resultados"
                : $"Mostrando {desde}–{hasta} de {_totalRegistros} productos";

            RenderizarPaginacion();
        }

        private void RenderizarPaginacion()
        {
            PanelPaginacion.Children.Clear();
            var totalPaginas = (int)Math.Ceiling(_totalRegistros / (double)PorPagina);
            if (totalPaginas <= 1) return;

            AgregarBtnPag("Ant.", _paginaActual > 1, () => CambiarPagina(_paginaActual - 1));

            for (int i = 1; i <= Math.Min(3, totalPaginas); i++)
            {
                var p = i;
                AgregarBtnPag(p.ToString(), true, () => CambiarPagina(p), p == _paginaActual);
            }

            if (totalPaginas > 4)
            {
                var dots = new TextBlock
                {
                    Text = "...",
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Colors.Gray),
                    Margin = new Thickness(4, 0, 4, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                PanelPaginacion.Children.Add(dots);
                var last = totalPaginas;
                AgregarBtnPag(last.ToString(), true, () => CambiarPagina(last));
            }

            AgregarBtnPag("Sig.", _paginaActual < totalPaginas, () => CambiarPagina(_paginaActual + 1));
        }

        private void AgregarBtnPag(string texto, bool habilitado,
                                    Action accion, bool activo = false)
        {
            var btn = new Button
            {
                Content = texto,
                IsEnabled = habilitado,
                Margin = new Thickness(2, 0, 2, 0),
                Padding = new Thickness(10, 4, 10, 4),
                FontSize = 12,
                Cursor = Cursors.Hand,
                Background = activo
                    ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E7D32"))
                    : new SolidColorBrush(Colors.Transparent),
                Foreground = activo
                    ? new SolidColorBrush(Colors.White)
                    : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#666666")),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DDDDDD")),
                BorderThickness = new Thickness(0.5),
            };
            btn.Click += (_, _) => accion();
            PanelPaginacion.Children.Add(btn);
        }

        private void CambiarPagina(int pagina)
        {
            _paginaActual = pagina;
            CargarProductos();
        }

        private void TxtBusqueda_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_placeholderActivo) return;
            _paginaActual = 1;
            CargarProductos();
        }

        private void TxtBusqueda_GotFocus(object sender, RoutedEventArgs e)
        {
            if (_placeholderActivo)
            {
                TxtBusqueda.Text = "";
                TxtBusqueda.Foreground = new SolidColorBrush(Colors.Black);
                _placeholderActivo = false;
            }
        }

        private void TxtBusqueda_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtBusqueda.Text))
            {
                _placeholderActivo = true;
                TxtBusqueda.Text = TxtBusqueda.Tag?.ToString();
                TxtBusqueda.Foreground = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#AAAAAA"));
            }
        }

        private void CmbCategoria_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _paginaActual = 1;
            CargarProductos();
        }

        private void BtnEditar_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int id)
            {
                var producto = _repo.ObtenerPorId(id);
                if (producto == null) return;

                var ventana = new ElApostol_WPF.UI.Dueño.AgregarEditarProductoWindow(producto);
                ventana.Owner = this;
                if (ventana.ShowDialog() == true)
                {
                    CargarMetricas();
                    CargarProductos();
                }
            }
        }

        private void BtnInventario_Click(object sender, RoutedEventArgs e)
        {
            _paginaActual = 1;
            CargarMetricas();
            CargarProductos();
        }

        private void BtnAgregarMaterial_Click(object sender, RoutedEventArgs e)
        {
            var ventana = new ElApostol_WPF.UI.Dueño.AgregarEditarProductoWindow();
            ventana.Owner = this;
            if (ventana.ShowDialog() == true)
            {
                CargarMetricas();
                CargarProductos();
            }
        }

        private void BtnNuevaVenta_Click(object sender, RoutedEventArgs e)
        {
            var ventana = new ElApostol_WPF.UI.Shared.NuevaVentaWindow(_usuario);
            this.Hide();
            ventana.Closed += (s, args) =>
            {
                this.Show();
            };
            if (ventana.ShowDialog() == true)
            {
                CargarMetricas();
            }
        }

        private void BtnReporteVentas_Click(object sender, RoutedEventArgs e)
        {
            var w = new ReporteVentasWindow(_usuario);
            w.Show();
            Close();
        }

        private void BtnGestionEmpleados_Click(object sender, RoutedEventArgs e)
        {
            var ventana = new GestionEmpleadosWindow(_usuario);
            ventana.Show();
            Close();
        }

        private void BtnCambiarClaveEmpleado_Click(object sender, RoutedEventArgs e)
        {
            var ventana = new CambiarContrasenaEmpleadoWindow(_usuario);
            ventana.Show();
        }

        private void BtnCerrarSesion_Click(object sender, RoutedEventArgs e)
        {
            var login = new LoginWindow();
            login.Show();
            this.Close();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.F1: BtnInventario_Click(null, null); break;
                case Key.F2: BtnAgregarMaterial_Click(null, null); break;
                case Key.F3: BtnNuevaVenta_Click(null, null); break;
                case Key.F4: BtnReporteVentas_Click(null, null); break;
                case Key.F5: BtnGestionEmpleados_Click(null, null); break;
                case Key.F6: BtnCambiarClaveEmpleado_Click(null, null); break;
                case Key.F12: BtnCerrarSesion_Click(null, null); break;
            }
        }

        private static string ObtenerIniciales(string nombre)
        {
            var partes = nombre.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return partes.Length >= 2
                ? $"{partes[0][0]}{partes[1][0]}".ToUpper()
                : nombre[..Math.Min(2, nombre.Length)].ToUpper();
        }
    }

    public class ProductoRow
    {
        public int Id { get; }
        public string CodigoInterno { get; }
        public string Nombre { get; }
        public string Marca { get; }
        public string NombreCategoria { get; }
        public string PrecioDisplay { get; }
        public string MedidaDisplay { get; }
        public string StockDisplay { get; }
        public Brush StockBadgeBg { get; }
        public Brush StockBadgeFg { get; }

        public ProductoRow(Producto p)
        {
            var v = p.Variantes.FirstOrDefault();

            Id = p.Id;
            CodigoInterno = p.CodigoInterno;
            Nombre = p.Nombre;
            Marca = p.Marca ?? "-";
            NombreCategoria = p.NombreCategoria ?? "-";
            PrecioDisplay = v != null ? $"${v.PrecioUnitario:N0}" : "-";
            MedidaDisplay = v?.Medida ?? "-";
            StockDisplay = v?.StockActual.ToString() ?? "-";

            if (v == null)
            {
                StockBadgeBg = new SolidColorBrush(Colors.LightGray);
                StockBadgeFg = new SolidColorBrush(Colors.Gray);
            }
            else if (v.StockActual < v.StockMinimo / 2)
            {
                StockBadgeBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FCEBEB"));
                StockBadgeFg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#791F1F"));
            }
            else if (v.StockActual < v.StockMinimo)
            {
                StockBadgeBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FAEEDA"));
                StockBadgeFg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#633806"));
            }
            else
            {
                StockBadgeBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EAF3DE"));
                StockBadgeFg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27500A"));
            }
        }
    }
}