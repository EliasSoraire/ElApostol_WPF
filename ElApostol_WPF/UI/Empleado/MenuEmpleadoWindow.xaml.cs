using ElApostol_WPF.Data;
using ElApostol_WPF.Models;
using ElApostol_WPF.UI.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace ElApostol_WPF.UI.Empleado
{
    public class VarianteVM
    {
        public int VarianteId { get; set; }
        public int ProductoId { get; set; }
        public string CodigoInterno { get; set; } = "";
        public string Nombre { get; set; } = "";
        public string Marca { get; set; } = "—";
        public string NombreCategoria { get; set; } = "";
        public string Medida { get; set; } = "";
        public decimal PrecioUnitario { get; set; }
        public int StockActual { get; set; }
        public int StockMinimo { get; set; }

        public string PrecioDisplay => $"${PrecioUnitario:N0}";
        public bool TieneStock => StockActual > 0;
        public string AccionLabel => TieneStock ? "Vender" : "Sin stock";
        public string StockEstado
        {
            get
            {
                if (StockActual <= 0 || StockActual <= StockMinimo / 2) return "crit";
                if (StockActual <= StockMinimo) return "low";
                return "ok";
            }
        }
    }

    public partial class MenuEmpleadoWindow : Window
    {
        private readonly Usuario _usuario;
        private readonly ProductoRepository _productoRepo = new();
        private readonly VentaRepository _ventaRepo = new();

        private const int PageSize = 10;
        private int _paginaActual = 1;
        private int _totalRegistros = 0;
        private int TotalPaginas => Math.Max(1, (int)Math.Ceiling(_totalRegistros / (double)PageSize));

        private List<Categoria> _categorias = new();
        private readonly DispatcherTimer _relojTimer = new();

        public MenuEmpleadoWindow(Usuario usuario)
        {
            InitializeComponent();
            _usuario = usuario;

            InicializarUI();
            CargarCategorias();
            CargarPagina();
            CargarResumenTurno();
            IniciarReloj();

            CtrlNuevaVenta.SalirSolicitado += CtrlNuevaVenta_SalirSolicitado;
        }

        private void OcultarTodosLosPaneles()
        {
            PanelInventario.Visibility = Visibility.Collapsed;
            CtrlNuevaVenta.Visibility = Visibility.Collapsed;
            CtrlRegistroCaja.Visibility = Visibility.Collapsed;

            var inactivo = (Style)FindResource("SbBtn");
            BtnNavInventario.Style = inactivo;
            BtnNavNuevaVenta.Style = inactivo;
            BtnNavRegistroCaja.Style = inactivo;
        }

        private void MostrarInventario()
        {
            OcultarTodosLosPaneles();
            PanelInventario.Visibility = Visibility.Visible;
            BtnNavInventario.Style = (Style)FindResource("SbBtnActive");
            CargarPagina();
            CargarResumenTurno();
        }

        public void MostrarNuevaVenta(int? varianteId = null)
        {
            OcultarTodosLosPaneles();
            CtrlNuevaVenta.Visibility = Visibility.Visible;
            BtnNavNuevaVenta.Style = (Style)FindResource("SbBtnActive");
            CtrlNuevaVenta.Inicializar(_usuario, varianteId);
        }

        private void MostrarRegistroCaja()
        {
            OcultarTodosLosPaneles();
            CtrlRegistroCaja.Visibility = Visibility.Visible;
            BtnNavRegistroCaja.Style = (Style)FindResource("SbBtnActive");
            CtrlRegistroCaja.Inicializar(_usuario);
        }

        private void CtrlNuevaVenta_SalirSolicitado(object? sender, EventArgs e) =>
            MostrarInventario();


        private void InicializarUI()
        {
            var partes = _usuario.NombreCompleto.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var iniciales = partes.Length >= 2
                ? $"{partes[0][0]}{partes[1][0]}".ToUpper()
                : _usuario.NombreCompleto.Substring(0, Math.Min(2, _usuario.NombreCompleto.Length)).ToUpper();

            TxtAvatarIniciales.Text = iniciales;
            TxtNombreCompleto.Text = _usuario.NombreCompleto;

            var ingreso = _usuario.HoraInicioSesion ?? DateTime.Now;
            TxtIngresaste.Text = $"Ingresaste a las {ingreso:HH:mm} hs";

            TxtFechaHoy.Text = DateTime.Now.ToString(
                "dddd d 'de' MMMM, yyyy",
                new System.Globalization.CultureInfo("es-AR"));
        }

        private void IniciarReloj()
        {
            ActualizarReloj();
            _relojTimer.Interval = TimeSpan.FromSeconds(30);
            _relojTimer.Tick += (_, _) => ActualizarReloj();
            _relojTimer.Start();
        }

        private void ActualizarReloj() =>
            TxtReloj.Text = DateTime.Now.ToString("HH:mm");


        private void CargarCategorias()
        {
            try
            {
                _categorias = _productoRepo.ObtenerCategorias();
                CmbCategoria.Items.Clear();
                CmbCategoria.Items.Add("Todas las categorías");
                foreach (var c in _categorias)
                    CmbCategoria.Items.Add(c.Nombre);
                CmbCategoria.SelectedIndex = 0;
            }
            catch (Exception ex) { MostrarError("Error al cargar categorías", ex); }
        }

        private void CargarPagina()
        {
            try
            {
                var busqueda = TxtBuscar.Text.Trim();
                var catNombre = CmbCategoria.SelectedIndex > 0
                                  ? CmbCategoria.SelectedItem?.ToString()
                                  : null;
                var categoriaId = catNombre != null
                                  ? _categorias.FirstOrDefault(c => c.Nombre == catNombre)?.Id
                                  : null;

                var productos = _productoRepo.ObtenerPaginado(
                    busqueda, categoriaId, _paginaActual, PageSize, out _totalRegistros);

                var filas = productos
                    .SelectMany(p => p.Variantes.Select(v => new VarianteVM
                    {
                        VarianteId = v.Id,
                        ProductoId = p.Id,
                        CodigoInterno = p.CodigoInterno,
                        Nombre = p.Nombre,
                        Marca = p.Marca ?? "—",
                        NombreCategoria = p.NombreCategoria ?? "",
                        Medida = v.Medida ?? "",
                        PrecioUnitario = v.PrecioUnitario,
                        StockActual = v.StockActual,
                        StockMinimo = v.StockMinimo,
                    }))
                    .ToList();

                ListaInventario.ItemsSource = filas;
                Dispatcher.BeginInvoke(new Action(AjustarColumnaMaterial),
                    System.Windows.Threading.DispatcherPriority.ContextIdle);

                int desde = _totalRegistros == 0 ? 0 : (_paginaActual - 1) * PageSize + 1;
                int hasta = Math.Min(_paginaActual * PageSize, _totalRegistros);
                TxtPaginacionInfo.Text = $"Mostrando {desde}–{hasta} de {_totalRegistros} productos";

                RenderizarBotonesPaginacion();
                ActualizarAlertaCriticos();
            }
            catch (Exception ex) { MostrarError("Error al cargar inventario", ex); }
        }

        private void CargarResumenTurno()
        {
            try
            {
                var inicio = _usuario.HoraInicioSesion ?? DateTime.Today;
                var resumen = _ventaRepo.ObtenerResumenTurno(_usuario.Id, inicio);

                TxtVentasHoy.Text = resumen.CantidadVentas.ToString();
                TxtTotalRecaudado.Text = $"${resumen.TotalRecaudado:N0}";

                if (resumen.UltimaVenta != null)
                {
                    TxtUltimaVentaHora.Text = resumen.UltimaVenta.FechaHora.ToString("HH:mm");
                    TxtUltimaVentaDesc.Text = resumen.UltimaVentaDesc ?? "";
                }
                else
                {
                    TxtUltimaVentaHora.Text = "—";
                    TxtUltimaVentaDesc.Text = "Sin ventas aún";
                }
            }
            catch
            {
                TxtVentasHoy.Text = "—";
                TxtTotalRecaudado.Text = "—";
                TxtUltimaVentaHora.Text = "—";
                TxtUltimaVentaDesc.Text = "";
            }
        }

        private void ActualizarAlertaCriticos()
        {
            try
            {
                var criticos = _productoRepo.ObtenerVariantesCriticas();
                if (criticos.Count > 0)
                {
                    const int maxMostrar = 3;
                    var lista = string.Join(", ", criticos.Take(maxMostrar));
                    var resto = criticos.Count > maxMostrar ? $" y {criticos.Count - maxMostrar} más" : "";

                    TxtAlerta.Text = $"Stock crítico: {lista}{resto}";
                    AlertaBanner.ToolTip = string.Join("\n", criticos);
                    AlertaBanner.Visibility = Visibility.Visible;
                }
                else
                {
                    AlertaBanner.Visibility = Visibility.Collapsed;
                }
            }
            catch { AlertaBanner.Visibility = Visibility.Collapsed; }
        }


        private void RenderizarBotonesPaginacion()
        {
            PanelPagBtns.Children.Clear();

            void Agregar(string label, int pagina, bool activa)
            {
                var btn = new Button
                {
                    Content = label,
                    Style = activa ? (Style)FindResource("PagBtnActive") : (Style)FindResource("PagBtn"),
                    IsEnabled = pagina >= 1 && pagina <= TotalPaginas,
                    Margin = new Thickness(2, 0, 2, 0)
                };
                btn.Click += (_, _) => { _paginaActual = pagina; CargarPagina(); };
                PanelPagBtns.Children.Add(btn);
            }

            TextBlock Puntos() => new TextBlock
            {
                Text = "...",
                FontSize = 12,
                Foreground = System.Windows.Media.Brushes.Gray,
                Margin = new Thickness(4, 0, 4, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            Agregar("Ant.", _paginaActual - 1, false);

            int start = Math.Max(1, _paginaActual - 2);
            int end = Math.Min(TotalPaginas, start + 4);
            start = Math.Max(1, end - 4);

            if (start > 1) { Agregar("1", 1, false); if (start > 2) PanelPagBtns.Children.Add(Puntos()); }
            for (int i = start; i <= end; i++) Agregar(i.ToString(), i, i == _paginaActual);
            if (end < TotalPaginas) { if (end < TotalPaginas - 1) PanelPagBtns.Children.Add(Puntos()); Agregar(TotalPaginas.ToString(), TotalPaginas, false); }

            Agregar("Sig.", _paginaActual + 1, false);
        }


        private void TxtBuscar_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (TxtBuscarPlaceholder != null)
                TxtBuscarPlaceholder.Visibility = string.IsNullOrEmpty(TxtBuscar.Text)
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            _paginaActual = 1;
            CargarPagina();
        }

        private void CmbCategoria_SelectionChanged(object sender, SelectionChangedEventArgs e)
        { _paginaActual = 1; CargarPagina(); }

        private void GridHeader_Click(object sender, RoutedEventArgs e) { }

        private void ListaInventario_SizeChanged(object sender, SizeChangedEventArgs e) => AjustarColumnaMaterial();

        private void AjustarColumnaMaterial()
        {
            if (ListaInventario.View is not System.Windows.Controls.GridView gridView) return;

            double otrasColumnas = 0;
            foreach (var col in gridView.Columns)
            {
                if (col != ColMaterial) otrasColumnas += col.ActualWidth > 0 ? col.ActualWidth : col.Width;
            }

            const double margen = 22;
            double disponible = ListaInventario.ActualWidth - otrasColumnas - margen;
            ColMaterial.Width = disponible > 100 ? disponible : 100;
        }

        private void BtnVender_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is VarianteVM vm)
                AbrirNuevaVenta(vm.VarianteId);
        }

        private void BtnNuevaVenta_Click(object sender, RoutedEventArgs e) =>
            AbrirNuevaVenta(null);

        private void AbrirNuevaVenta(int? varianteId) => MostrarNuevaVenta(varianteId);

        private void BtnNavInventario_Click(object sender, RoutedEventArgs e)
        {
            _paginaActual = 1;
            TxtBuscar.Clear();
            CmbCategoria.SelectedIndex = 0;
            MostrarInventario();
        }

        private void BtnNavNuevaVenta_Click(object sender, RoutedEventArgs e) =>
            AbrirNuevaVenta(null);

        private void BtnNavRegistroCaja_Click(object sender, RoutedEventArgs e) => MostrarRegistroCaja();

        private void BtnCerrarSesion_Click(object sender, RoutedEventArgs e)
        {
            var res = MessageBox.Show("¿Cerrás sesión?", "Cerrar sesión",
                                      MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res == MessageBoxResult.Yes)
            {
                _relojTimer.Stop();
                new UI.Login.LoginWindow().Show();
                Close();
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.F1: BtnNavInventario_Click(sender, e); break;
                case Key.F2: AbrirNuevaVenta(null); break;
                case Key.F3: BtnNavRegistroCaja_Click(sender, e); break;
                case Key.F12: BtnCerrarSesion_Click(sender, e); break;
            }
        }

        private void MostrarError(string msg, Exception ex) =>
            MessageBox.Show($"{msg}:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}