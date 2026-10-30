using ElApostol_WPF.UI.Shared;
using ElApostol_WPF.Data;
using ElApostol_WPF.Models;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace ElApostol_WPF.UI.Empleado
{
    public partial class RegistroCajaControl : UserControl
    {
        private Usuario _usuario = null!;
        private readonly VentaRepository _ventaRepo = new();

        private readonly ObservableCollection<VentaCajaVM> _ventas = new();

        private string _periodoActual = "hoy";
        private DateTime? _fechaPersonalizada = null;

        private int _paginaActual = 1;
        private const int RegistrosPorPagina = 15;
        private int _totalRegistros = 0;
        private List<VentaCajaVM> _todasLasVentas = new();

        public RegistroCajaControl()
        {
            InitializeComponent();
            DgVentas.ItemsSource = _ventas;
        }

        public void Inicializar(Usuario usuario)
        {
            _usuario = usuario;
            _periodoActual = "hoy";
            _fechaPersonalizada = null;
            MarcarPeriodo("hoy");

            DpFecha.SelectedDateChanged -= DpFecha_SelectedDateChanged;
            DpFecha.SelectedDate = DateTime.Today;
            DpFecha.SelectedDateChanged += DpFecha_SelectedDateChanged;

            CargarDatos();
        }

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
                Dialogo.Show($"Error al cargar el registro:\n{ex.Message}",
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private (DateTime desde, DateTime hasta) ObtenerRangoFechas()
        {
            return _periodoActual switch
            {
                "hoy" => (DateTime.Today, DateTime.Today.AddDays(1).AddTicks(-1)),
                "ayer" => (DateTime.Today.AddDays(-1), DateTime.Today.AddTicks(-1)),
                // Lunes de la semana actual (el domingo pertenece a la semana que empezó el lunes anterior)
                "semana" => (DateTime.Today.AddDays(-(((int)DateTime.Today.DayOfWeek + 6) % 7)),
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
            if (sender is not Button btn) return;
            var tag = btn.Tag?.ToString() ?? "hoy";
            MarcarPeriodo(tag);
            CargarDatos();
        }

        private void DpFecha_SelectedDateChanged(object? sender, SelectionChangedEventArgs e)
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
            if (sender is not Button btn) return;
            if (btn.Tag is not int ventaId) return;

            try
            {
                var venta = _ventaRepo.ObtenerVentaConDetalle(ventaId);
                if (venta == null) return;

                var ventana = new DetalleVentasWindow(
                    $"Venta {venta.NumeroComprobante}",
                    $"{venta.FechaHora:dd/MM/yyyy HH:mm} · {venta.MetodoPagoDisplay} · {venta.Unidades} unidad{(venta.Unidades != 1 ? "es" : "")}",
                    new List<VentaDetallada> { venta })
                {
                    Owner = Window.GetWindow(this)
                };
                ventana.ShowDialog();
            }
            catch (Exception ex)
            {
                Dialogo.Show($"No se pudo cargar el detalle:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnExportar_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var (desde, hasta) = ObtenerRangoFechas();
                var ventas = _ventaRepo.ObtenerVentasConDetalle(desde, hasta.AddTicks(1), _usuario.Id);

                if (ventas.Count == 0 &&
                    !Dialogo.Confirmar("No hay ventas en el período seleccionado.\n¿Querés generar el cierre igual (en $0)?",
                        "Cierre de caja", "Generar igual", "Cancelar"))
                    return;

                var periodo = _periodoActual switch
                {
                    "hoy" => $"Hoy, {desde:dd/MM/yyyy}",
                    "ayer" => $"Ayer, {desde:dd/MM/yyyy}",
                    "semana" => $"Semana del {desde:dd/MM/yyyy} al {hasta:dd/MM/yyyy}",
                    _ => $"Día {desde:dd/MM/yyyy}"
                };

                var carpeta = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Cierres de caja");
                System.IO.Directory.CreateDirectory(carpeta);

                var nombreArchivo = desde.Date == hasta.Date
                    ? $"cierre_caja_{_usuario.NombreUsuario}_{desde:yyyyMMdd}.pdf"
                    : $"cierre_caja_{_usuario.NombreUsuario}_{desde:yyyyMMdd}_{hasta:yyyyMMdd}.pdf";

                var dialogo = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Guardar cierre de caja",
                    Filter = "Documento PDF (*.pdf)|*.pdf",
                    InitialDirectory = carpeta,
                    FileName = nombreArchivo,
                    DefaultExt = ".pdf"
                };
                if (dialogo.ShowDialog(Window.GetWindow(this)) != true) return;

                Reports.CierreCajaPdf.Generate(dialogo.FileName, new Reports.CierreCajaData
                {
                    Empleado = _usuario.NombreCompleto,
                    Usuario = _usuario.NombreUsuario,
                    Periodo = periodo,
                    FechaDesde = desde,
                    FechaHasta = hasta,
                    FechaGeneracion = DateTime.Now,
                    Ventas = ventas
                });

                if (Dialogo.Confirmar($"El cierre de caja se guardó en:\n{dialogo.FileName}\n\n¿Querés abrirlo ahora?",
                        "Cierre de caja generado", "Abrir PDF", "Cerrar", MessageBoxImage.Information))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = dialogo.FileName,
                        UseShellExecute = true
                    });
                }
            }
            catch (System.IO.IOException ex)
            {
                Dialogo.Show($"No se pudo guardar el PDF. Si lo tenés abierto, cerralo e intentá de nuevo.\n\n{ex.Message}",
                    "Cierre de caja", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                Dialogo.Show($"Error al generar el cierre de caja:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
