using ElApostol_WPF.Data;
using ElApostol_WPF.Models;
using ElApostol_WPF.UI.Dueño;
using ElApostol_WPF.UI.Shared;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;

namespace ElApostol_WPF.UI.Empleado
{
    public partial class RegistroCajaWindow : Window
    {
        private readonly Usuario _usuario;
        private readonly VentaRepository _ventaRepo = new();
        private readonly ProductoRepository _productoRepo = new();

        private readonly ObservableCollection<VentaCajaVM> _ventas = new();
        private readonly DispatcherTimer _relojTimer = new();

        private string _periodoActual = "hoy";        
        private DateTime? _fechaPersonalizada = null;

        private int _paginaActual = 1;
        private const int RegistrosPorPagina = 15;
        private int _totalRegistros = 0;
        private List<VentaCajaVM> _todasLasVentas = new();


        public RegistroCajaWindow(Usuario usuario)
        {
            InitializeComponent();
            _usuario = usuario;

            InicializarUI();
            InicializarReloj();
            CargarDatos();
        }

        private void InicializarUI()
        {
            var partes = _usuario.NombreCompleto.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            TxtAvatar.Text = partes.Length >= 2
                ? $"{partes[0][0]}{partes[1][0]}".ToUpper()
                : _usuario.NombreCompleto[..Math.Min(2, _usuario.NombreCompleto.Length)].ToUpper();

            TxtSbNombre.Text = _usuario.NombreCompleto;
            TxtTurnoDesde.Text = $"Ingresaste a las {_usuario.HoraInicioSesion:HH:mm} hs";
            
            DgVentas.ItemsSource = _ventas;

            DpFecha.SelectedDateChanged -= DpFecha_SelectedDateChanged;
            DpFecha.SelectedDate = DateTime.Today;
            DpFecha.SelectedDateChanged += DpFecha_SelectedDateChanged;
        }

        private void InicializarReloj()
        {
            ActualizarReloj();
            _relojTimer.Interval = TimeSpan.FromSeconds(30);
            _relojTimer.Tick += (_, _) => ActualizarReloj();
            _relojTimer.Start();
        }

        private void ActualizarReloj() =>
            TxtReloj.Text = DateTime.Now.ToString("HH:mm");

        private void CargarDatos()
        {
            try
            {
                var (desde, hasta) = ObtenerRangoFechas();
                _todasLasVentas = _ventaRepo.ObtenerVentasDetalladas(_usuario.Id, desde, hasta);
                _totalRegistros = _todasLasVentas.Count;
                _paginaActual = 1;

                ActualizarMetricas();
                ActualizarTabla();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar el registro:\n{ex.Message}",
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private (DateTime desde, DateTime hasta) ObtenerRangoFechas()
        {
            return _periodoActual switch
            {
                "hoy" => (DateTime.Today, DateTime.Today.AddDays(1).AddTicks(-1)),
                "ayer" => (DateTime.Today.AddDays(-1), DateTime.Today.AddTicks(-1)),
                "semana" => (DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek + 1),
                             DateTime.Today.AddDays(1).AddTicks(-1)),
                "fecha" => (_fechaPersonalizada!.Value,
                             _fechaPersonalizada!.Value.AddDays(1).AddTicks(-1)),
                _ => (DateTime.Today, DateTime.Today.AddDays(1).AddTicks(-1))
            };
        }

        private void ActualizarMetricas()
        {
            var total = _todasLasVentas.Sum(v => v.Total);
            var efectivo = _todasLasVentas.Where(v => v.MetodoPago == "efectivo");
            var tarjeta = _todasLasVentas.Where(v => v.MetodoPago == "tarjeta");
            var transf = _todasLasVentas.Where(v => v.MetodoPago == "transferencia");

            var cntEf = efectivo.Count();
            var cntTj = tarjeta.Count();
            var cntTr = transf.Count();

            TxtTotalRecaudado.Text = $"${total:N0}";
            TxtSubTotalRecaudado.Text = $"{_totalRegistros} venta{(_totalRegistros != 1 ? "s" : "")} del período";

            TxtTotalEfectivo.Text = $"${efectivo.Sum(v => v.Total):N0}";
            TxtSubEfectivo.Text = $"{cntEf} transacción{(cntEf != 1 ? "es" : "")}";

            TxtTotalTarjeta.Text = $"${tarjeta.Sum(v => v.Total):N0}";
            TxtSubTarjeta.Text = cntTj > 0
                ? $"{cntTj} transacción{(cntTj != 1 ? "es" : "")} · con recargo"
                : "0 transacciones";

            TxtTotalTransferencia.Text = $"${transf.Sum(v => v.Total):N0}";
            TxtSubTransferencia.Text = $"{cntTr} transacción{(cntTr != 1 ? "es" : "")}";

            var labelPeriodo = _periodoActual switch
            {
                "hoy" => "hoy",
                "ayer" => "ayer",
                "semana" => "esta semana",
                "fecha" => _fechaPersonalizada?.ToString("dd/MM/yyyy") ?? "",
                _ => ""
            };
            TxtConteoVentas.Text = $"{_totalRegistros} venta{(_totalRegistros != 1 ? "s" : "")} registrada{(_totalRegistros != 1 ? "s" : "")} {labelPeriodo}";
        }

        private void ActualizarTabla()
        {
            _ventas.Clear();

            var pagina = _todasLasVentas
                .Skip((_paginaActual - 1) * RegistrosPorPagina)
                .Take(RegistrosPorPagina)
                .ToList();

            foreach (var v in pagina)
                _ventas.Add(v);

            ActualizarPaginacion();
        }

        private void ActualizarPaginacion()
        {
            var totalPaginas = (int)Math.Ceiling(_totalRegistros / (double)RegistrosPorPagina);
            if (totalPaginas == 0) totalPaginas = 1;

            TxtPaginaActual.Text = _paginaActual.ToString();

            var desde = (_paginaActual - 1) * RegistrosPorPagina + 1;
            var hasta = Math.Min(_paginaActual * RegistrosPorPagina, _totalRegistros);
            TxtPaginaInfo.Text = _totalRegistros == 0
                ? "Sin registros"
                : $"Mostrando {desde}–{hasta} de {_totalRegistros} ventas del período";

            BtnPagAnt.IsEnabled = _paginaActual > 1;
            BtnPagSig.IsEnabled = _paginaActual < totalPaginas;
        }

        private void MarcarPeriodo(string periodo)
        {
            _periodoActual = periodo;

            BtnPeriodoHoy.Style = periodo == "hoy" ? (Style)Resources["BtnPeriodoActivo"] : (Style)Resources["BtnPeriodo"];
            BtnPeriodoAyer.Style = periodo == "ayer" ? (Style)Resources["BtnPeriodoActivo"] : (Style)Resources["BtnPeriodo"];
            BtnPeriodoSemana.Style = periodo == "semana" ? (Style)Resources["BtnPeriodoActivo"] : (Style)Resources["BtnPeriodo"];
        }

        private void BtnPeriodo_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.Button btn) return;
            var tag = btn.Tag?.ToString() ?? "hoy";
            MarcarPeriodo(tag);
            CargarDatos();
        }

        private void DpFecha_SelectedDateChanged(object? sender,
            System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (DpFecha.SelectedDate is DateTime fecha)
            {
                _fechaPersonalizada = fecha;
                MarcarPeriodo("fecha");
                CargarDatos();
            }
        }

        private void BtnPagAnt_Click(object sender, RoutedEventArgs e)
        {
            if (_paginaActual <= 1) return;
            _paginaActual--;
            ActualizarTabla();
        }

        private void BtnPagSig_Click(object sender, RoutedEventArgs e)
        {
            var totalPaginas = (int)Math.Ceiling(_totalRegistros / (double)RegistrosPorPagina);
            if (_paginaActual >= totalPaginas) return;
            _paginaActual++;
            ActualizarTabla();
        }

        private void BtnVerDetalle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.Button btn) return;
            if (btn.Tag is not int ventaId) return;

            var vm = _todasLasVentas.FirstOrDefault(v => v.VentaId == ventaId);
            if (vm == null) return;

            MessageBox.Show(
                $"Comprobante: {vm.NumeroComprobante}\n" +
                $"Fecha: {vm.FechaHora:dd/MM/yyyy HH:mm}\n" +
                $"Método: {vm.MetodoPagoDisplay}\n" +
                $"Total: {vm.TotalDisplay}\n" +
                $"Ítems: {vm.CantidadItems}",
                "Detalle de venta",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void BtnSbNuevaVenta_Click(object sender, RoutedEventArgs e)
        {
            var ventana = new NuevaVentaWindow(_usuario);
            ventana.Owner = this;
            ventana.ShowDialog();
            CargarDatos();
        }

        private void BtnSbInventario_Click(object sender, RoutedEventArgs e)
        {
            var ventana = new MenuEmpleadoWindow(_usuario);
            this.Hide();
            ventana.Closed += (s, args) =>
            {
                this.Show();
            };
            if (ventana.ShowDialog() == true)
            {
            }
        }

        private void BtnExportar_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("La exportación a PDF estará disponible próximamente.",
                            "Exportar", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnCerrarSesion_Click(object sender, RoutedEventArgs e)
        {
            var confirmar = MessageBox.Show(
                "¿Estás seguro de que querés cerrar tu sesión?",
                "Cerrar sesión",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirmar == MessageBoxResult.Yes)
            {
                _relojTimer.Stop();
                DialogResult = true;
                Close();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _relojTimer.Stop();
            base.OnClosed(e);
        }
    }

    public class VentaCajaVM
    {
        public int VentaId { get; init; }
        public string NumeroComprobante { get; init; } = "";
        public DateTime FechaHora { get; init; }
        public string MetodoPago { get; init; } = "";
        public decimal Total { get; init; }
        public int CantidadItems { get; init; }
        public string PrimerMaterial { get; init; } = "";
        public string MedidaDisplay { get; init; } = "";

        public string HoraDisplay => FechaHora.ToString("HH:mm");
        public string TotalDisplay => $"${Total:N0}";
        public string MetodoPagoDisplay => MetodoPago switch
        {
            "efectivo" => "Efectivo",
            "tarjeta" => "Tarjeta",
            "transferencia" => "Transferencia",
            _ => MetodoPago
        };
    }
}