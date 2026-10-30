using ElApostol_WPF.Models;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace ElApostol_WPF.UI.Shared
{
    /// <summary>
    /// Muestra ventas agrupadas por vendedor; cada venta se despliega para ver sus renglones.
    /// Se usa para el detalle de un día (Dueño) y para el detalle de una venta (Empleado).
    /// </summary>
    public partial class DetalleVentasWindow : Window
    {
        private static readonly CultureInfo _ci = new("es-AR");
        private readonly List<GrupoVentasVM> _grupos;

        public DetalleVentasWindow(string titulo, string subtitulo, List<VentaDetallada> ventas)
        {
            InitializeComponent();

            TxtTitulo.Text = titulo;
            Title = titulo;
            TxtSubtitulo.Text = subtitulo;

            TxtTotal.Text = Moneda(ventas.Sum(v => v.Total));
            TxtVentas.Text = ventas.Count.ToString();
            TxtEfectivo.Text = Moneda(ventas.Where(v => v.MetodoPago == "efectivo").Sum(v => v.Total));
            TxtTransferencia.Text = Moneda(ventas.Where(v => v.MetodoPago == "transferencia").Sum(v => v.Total));
            TxtTarjeta.Text = Moneda(ventas.Where(v => v.MetodoPago == "tarjeta").Sum(v => v.Total));

            // Una sola venta: se muestra abierta. Un solo vendedor: su grupo abierto.
            var unaVenta = ventas.Count == 1;
            _grupos = ventas
                .GroupBy(v => new { v.UsuarioId, v.Empleado })
                .Select(g => new GrupoVentasVM(g.Key.Empleado,
                    g.OrderBy(v => v.FechaHora).Select(v => new VentaVM(v) { Expandido = unaVenta }).ToList()))
                .OrderByDescending(g => g.Total)
                .ToList();

            if (_grupos.Count == 1) _grupos[0].Expandido = true;

            ListaGrupos.ItemsSource = _grupos;
            TxtVacio.Visibility = ventas.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        internal static string Moneda(decimal valor) => valor.ToString("C0", _ci);

        private void BtnExpandir_Click(object sender, RoutedEventArgs e) => CambiarTodo(true);

        private void BtnContraer_Click(object sender, RoutedEventArgs e) => CambiarTodo(false);

        private void CambiarTodo(bool expandir)
        {
            foreach (var g in _grupos)
            {
                g.Expandido = expandir;
                foreach (var v in g.Ventas) v.Expandido = expandir;
            }
        }

        private void BtnCerrar_Click(object sender, RoutedEventArgs e) => Close();

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) Close();
        }
    }

    public abstract class ExpandibleVM : INotifyPropertyChanged
    {
        private bool _expandido;
        public bool Expandido
        {
            get => _expandido;
            set
            {
                if (_expandido == value) return;
                _expandido = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Expandido)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    public class GrupoVentasVM : ExpandibleVM
    {
        public string Nombre { get; }
        public string Iniciales { get; }
        public string Resumen { get; }
        public decimal Total { get; }
        public string TotalDisplay { get; }
        public List<VentaVM> Ventas { get; }

        public GrupoVentasVM(string nombre, List<VentaVM> ventas)
        {
            Nombre = nombre;
            Ventas = ventas;
            Total = ventas.Sum(v => v.Total);
            TotalDisplay = DetalleVentasWindow.Moneda(Total);

            var partes = nombre.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            Iniciales = partes.Length >= 2
                ? $"{partes[0][0]}{partes[1][0]}".ToUpper()
                : nombre[..Math.Min(2, nombre.Length)].ToUpper();

            var unidades = ventas.Sum(v => v.Unidades);
            Resumen = $"{ventas.Count} venta{(ventas.Count != 1 ? "s" : "")} · {unidades} unidad{(unidades != 1 ? "es" : "")}";
        }
    }

    public class VentaVM : ExpandibleVM
    {
        public string Comprobante { get; }
        public string Hora { get; }
        public string Metodo { get; }
        public Brush MetodoBg { get; }
        public Brush MetodoFg { get; }
        public string ResumenItems { get; }
        public decimal Total { get; }
        public int Unidades { get; }
        public string TotalDisplay { get; }
        public List<LineaVM> Lineas { get; }
        public string RecargoDisplay { get; }
        public Visibility RecargoVisibility { get; }
        public string ObservacionesDisplay { get; }
        public Visibility ObservacionesVisibility { get; }

        public VentaVM(VentaDetallada v)
        {
            Comprobante = v.NumeroComprobante;
            Hora = v.FechaHora.ToString("HH:mm");
            Metodo = v.MetodoPagoDisplay;
            Total = v.Total;
            Unidades = v.Unidades;
            TotalDisplay = DetalleVentasWindow.Moneda(v.Total);
            Lineas = v.Lineas.Select(l => new LineaVM(l)).ToList();

            var (bg, fg) = v.MetodoPago switch
            {
                "efectivo" => ("#EAF3DE", "#27500A"),
                "tarjeta" => ("#FAEEDA", "#633806"),
                _ => ("#E3EEFA", "#0D47A1"),
            };
            MetodoBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bg));
            MetodoFg = new SolidColorBrush((Color)ColorConverter.ConvertFromString(fg));

            var primero = v.Lineas.FirstOrDefault();
            ResumenItems = primero == null
                ? "Sin renglones"
                : $"{primero.Cantidad} × {primero.NombreProducto}" +
                  (v.Lineas.Count > 1 ? $"  +{v.Lineas.Count - 1} más" : "");

            RecargoDisplay = $"Subtotal {DetalleVentasWindow.Moneda(v.Subtotal)} + recargo tarjeta {DetalleVentasWindow.Moneda(v.MontoRecargo)}";
            RecargoVisibility = v.MontoRecargo > 0 ? Visibility.Visible : Visibility.Collapsed;
            ObservacionesDisplay = $"Obs.: {v.Observaciones}";
            ObservacionesVisibility = string.IsNullOrWhiteSpace(v.Observaciones) ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    public class LineaVM
    {
        public int Cantidad { get; }
        public string Producto { get; }
        public string Medida { get; }
        public string PrecioDisplay { get; }
        public string SubtotalDisplay { get; }

        public LineaVM(DetalleVenta d)
        {
            Cantidad = d.Cantidad;
            Producto = d.NombreProducto ?? "—";
            Medida = string.IsNullOrEmpty(d.Medida) ? "" : $" · {d.Medida}";
            PrecioDisplay = DetalleVentasWindow.Moneda(d.PrecioUnitarioHistorico);
            SubtotalDisplay = DetalleVentasWindow.Moneda(d.SubtotalLinea);
        }
    }
}
