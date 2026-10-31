using System.Windows;
using ElApostol_WPF.UI.Shared;
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

            CtrlNuevaVenta.SalirSolicitado += CtrlNuevaVenta_SalirSolicitado;
        }

        private void OcultarTodosLosPaneles()
        {
            PanelInventario.Visibility = Visibility.Collapsed;
            CtrlNuevaVenta.Visibility = Visibility.Collapsed;
            CtrlGestionEmpleados.Visibility = Visibility.Collapsed;
            CtrlReporteVentas.Visibility = Visibility.Collapsed;

            var inactivo = (Style)FindResource("SbBtn");
            BtnInventario.Style = inactivo;
            BtnNuevaVenta.Style = inactivo;
            BtnReporteVentas.Style = inactivo;
            BtnGestionEmpleados.Style = inactivo;
        }

        private void MostrarInventario()
        {
            OcultarTodosLosPaneles();
            PanelInventario.Visibility = Visibility.Visible;
            BtnInventario.Style = (Style)FindResource("SbBtnActive");
            CargarMetricas();
            CargarProductos();
        }

        public void MostrarNuevaVenta()
        {
            OcultarTodosLosPaneles();
            CtrlNuevaVenta.Visibility = Visibility.Visible;
            BtnNuevaVenta.Style = (Style)FindResource("SbBtnActive");
            CtrlNuevaVenta.Inicializar(_usuario);
        }

        private void MostrarGestionEmpleados()
        {
            OcultarTodosLosPaneles();
            CtrlGestionEmpleados.Visibility = Visibility.Visible;
            BtnGestionEmpleados.Style = (Style)FindResource("SbBtnActive");
            CtrlGestionEmpleados.Inicializar(_usuario);
        }

        private void MostrarReporteVentas()
        {
            OcultarTodosLosPaneles();
            CtrlReporteVentas.Visibility = Visibility.Visible;
            BtnReporteVentas.Style = (Style)FindResource("SbBtnActive");
            CtrlReporteVentas.Inicializar(_usuario);
        }

        private void CtrlNuevaVenta_SalirSolicitado(object? sender, EventArgs e)
        {
            _paginaActual = 1;
            MostrarInventario();
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

            var soloInactivos = ChkVerInactivos?.IsChecked == true;

            var filas = _repo.ObtenerInventarioPaginado(
                busqueda, categoriaId, _paginaActual, PorPagina, out _totalRegistros, soloInactivos);

            DgProductos.ItemsSource = filas.Select(f => new InventarioRow(f)).ToList();

            var desde = (_paginaActual - 1) * PorPagina + 1;
            var hasta = Math.Min(_paginaActual * PorPagina, _totalRegistros);
            TxtPaginaInfo.Text = _totalRegistros == 0
                ? "Sin resultados"
                : $"Mostrando {desde}–{hasta} de {_totalRegistros} variantes";

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

        private void ChkVerInactivos_Changed(object sender, RoutedEventArgs e)
        {
            _paginaActual = 1;
            CargarProductos();
        }

        private void BtnReactivar_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not int id) return;

            var producto = _repo.ObtenerPorId(id);
            if (producto == null) return;

            var res = Dialogo.Show(
                $"¿Reactivar \"{producto.Nombre}\" ({producto.CodigoInterno})?\n\n" +
                "Va a volver a aparecer en el inventario y en nuevas ventas, con el stock y precios que tenía.",
                "Reactivar producto", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (res != MessageBoxResult.Yes) return;

            try
            {
                _repo.ReactivarProducto(id);
                Dialogo.Show($"\"{producto.Nombre}\" fue reactivado.",
                    "Producto reactivado", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Dialogo.Show($"Error al reactivar:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            CargarMetricas();
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

        private void BtnEliminar_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not int id) return;

            var producto = _repo.ObtenerPorId(id);
            if (producto == null) return;

            if (_repo.TieneVentasAsociadas(id))
            {
                var res = Dialogo.Show(
                    $"\"{producto.Nombre}\" tiene ventas registradas, así que no se puede eliminar sin perder ese historial.\n\n" +
                    "¿Querés desactivarlo en su lugar? Va a dejar de aparecer en el inventario y en nuevas ventas, pero sus ventas anteriores se mantienen intactas.",
                    "No se puede eliminar",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (res != MessageBoxResult.Yes) return;

                _repo.DesactivarProducto(id);
                Dialogo.Show($"\"{producto.Nombre}\" fue desactivado.",
                    "Producto desactivado", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                var confirmar = Dialogo.Show(
                    $"¿Eliminar \"{producto.Nombre}\" y todas sus variantes? Esta acción no se puede deshacer.",
                    "Confirmar eliminación",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (confirmar != MessageBoxResult.Yes) return;

                _repo.EliminarProducto(id);
            }

            CargarMetricas();
            CargarProductos();
        }

        private void CardStockCritico_Click(object sender, MouseButtonEventArgs e)
        {
            var ventana = new StockCriticoWindow { Owner = this };
            ventana.ShowDialog();
        }

        private void BtnCategorias_Click(object sender, RoutedEventArgs e)
        {
            var ventana = new GestionCategoriasWindow { Owner = this };
            ventana.ShowDialog();
            if (ventana.HuboCambios)
            {
                var seleccion = (CmbCategoria.SelectedItem as ComboBoxItem)?.Tag as int?;
                CmbCategoria.SelectionChanged -= CmbCategoria_SelectionChanged;
                CargarCategorias();
                var item = CmbCategoria.Items.OfType<ComboBoxItem>().FirstOrDefault(i => i.Tag as int? == seleccion);
                CmbCategoria.SelectedItem = item ?? CmbCategoria.Items[0];
                CmbCategoria.SelectionChanged += CmbCategoria_SelectionChanged;
                CargarProductos();
            }
        }

        private void BtnInventario_Click(object sender, RoutedEventArgs e)
        {
            _paginaActual = 1;
            MostrarInventario();
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

        private void BtnNuevaVenta_Click(object sender, RoutedEventArgs e) => MostrarNuevaVenta();

        private void BtnReporteVentas_Click(object sender, RoutedEventArgs e) => MostrarReporteVentas();

        private void BtnGestionEmpleados_Click(object sender, RoutedEventArgs e) => MostrarGestionEmpleados();

        private void BtnCambiarMiClave_Click(object sender, RoutedEventArgs e)
        {
            var ventana = new CambiarMiContrasenaWindow(_usuario) { Owner = this };
            ventana.ShowDialog();
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
                case Key.F6: BtnCambiarMiClave_Click(null, null); break;
                case Key.F7: BtnCategorias_Click(null, null); break;
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

    public class InventarioRow
    {
        public int ProductoId { get; }
        public string CodigoInterno { get; }
        public string Nombre { get; }
        public string Marca { get; }
        public string NombreCategoria { get; }
        public string PrecioDisplay { get; }
        public string MedidaDisplay { get; }
        public string StockDisplay { get; }
        public Brush StockBadgeBg { get; }
        public Brush StockBadgeFg { get; }
        public Visibility VisibilidadActivo { get; }
        public Visibility VisibilidadInactivo { get; }

        public InventarioRow(FilaInventario f)
        {
            ProductoId = f.ProductoId;
            CodigoInterno = f.CodigoInterno;
            Nombre = f.Nombre;
            Marca = f.Marca ?? "-";
            NombreCategoria = f.NombreCategoria ?? "-";
            VisibilidadActivo = f.Activo ? Visibility.Visible : Visibility.Collapsed;
            VisibilidadInactivo = f.Activo ? Visibility.Collapsed : Visibility.Visible;

            if (f.VarianteId == null)
            {
                PrecioDisplay = "-";
                MedidaDisplay = "Sin medidas";
                StockDisplay = "-";
            }
            else
            {
                PrecioDisplay = $"${f.PrecioUnitario:N0}";
                MedidaDisplay = f.Medida ?? "-";
                StockDisplay = f.StockActual.ToString();
            }

            var (bg, fg) = f.StockEstado switch
            {
                "crit" => ("#FCEBEB", "#791F1F"),
                "low"  => ("#FAEEDA", "#633806"),
                "ok"   => ("#EAF3DE", "#27500A"),
                _      => ("#EEEEEE", "#888888"),
            };
            StockBadgeBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bg));
            StockBadgeFg = new SolidColorBrush((Color)ColorConverter.ConvertFromString(fg));
        }
    }
}
