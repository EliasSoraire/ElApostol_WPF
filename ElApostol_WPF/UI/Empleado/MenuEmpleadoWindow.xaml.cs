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
        public bool TieneStock => StockActual > StockMinimo;
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
        }


        private void InicializarUI()
        {
            var partes = _usuario.NombreCompleto.Split(' ');
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
                var (_, criticos, _) = _productoRepo.ObtenerMetricas();
                if (criticos > 0)
                {
                    TxtAlerta.Text = $"{criticos} producto{(criticos > 1 ? "s" : "")} con stock crítico — revisalos antes de ofrecer al cliente";
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
        { _paginaActual = 1; CargarPagina(); }

        private void CmbCategoria_SelectionChanged(object sender, SelectionChangedEventArgs e)
        { _paginaActual = 1; CargarPagina(); }

        private void GridHeader_Click(object sender, RoutedEventArgs e) { }

        private void BtnVender_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is VarianteVM vm)
                AbrirNuevaVenta(vm.VarianteId);
        }

        private void BtnNuevaVenta_Click(object sender, RoutedEventArgs e) =>
            AbrirNuevaVenta(null);

        private void AbrirNuevaVenta(int? varianteId)
        {
            var win = new NuevaVentaWindow(_usuario, varianteId);
            win.Owner = this;
            if (win.ShowDialog() == true)
            {
                CargarPagina();
                CargarResumenTurno();
            }
        }

        private void BtnNavInventario_Click(object sender, RoutedEventArgs e)
        {
            _paginaActual = 1;
            TxtBuscar.Clear();
            CmbCategoria.SelectedIndex = 0;
            CargarPagina();
        }

        private void BtnNavNuevaVenta_Click(object sender, RoutedEventArgs e) =>
            AbrirNuevaVenta(null);

        private void BtnNavRegistroCaja_Click(object sender, RoutedEventArgs e)
        {
            var win = new RegistroCajaWindow(_usuario);
            this.Hide();
            win.Closed += (s, args) =>
            {
                this.Show();
            };
            if (win.ShowDialog() == true)
            {
                
            }
        }

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