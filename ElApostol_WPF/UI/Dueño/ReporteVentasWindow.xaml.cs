using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ElApostol_WPF.Data;
using ElApostol_WPF.Models;
using Microsoft.Data.SqlClient;
using ElApostol_WPF.Reports;

namespace ElApostol_WPF.UI.Dueño
{
    public partial class ReporteVentasWindow : Window
    {
        private readonly Usuario _usuario;

        private class FilaDia
        {
            public DateTime Fecha { get; set; }
            public string DiaNombre { get; set; } = "";
            public string FechaDisplay { get; set; } = "";
            public string Empleado { get; set; } = "";
            public int CantidadVentas { get; set; }
            public decimal Total { get; set; }
            public string TotalDisplay { get; set; } = "";
            public string TicketDisplay { get; set; } = "";
            public bool EsMejorDia { get; set; }
            public Visibility BadgeMejorVisibility =>
                EsMejorDia ? Visibility.Visible : Visibility.Collapsed;
        }

        private List<FilaDia> _filas = new();
        private int _pagActual = 1;
        private const int PageSize = 10;

        private string _periodo = "semana";

        public ReporteVentasWindow(Usuario usuario)
        {
            InitializeComponent();
            _usuario = usuario;
            InicializarUI();
            CargarEmpleados();
            AplicarFiltros();
        }

        private void InicializarUI()
        {
            TxtNombreUsuario.Text = _usuario.NombreCompleto;
            var partes = _usuario.NombreCompleto.Split(' ');
            TxtAvatar.Text = partes.Length >= 2
                ? $"{partes[0][0]}{partes[1][0]}".ToUpper()
                : _usuario.NombreCompleto.Substring(0, Math.Min(2, _usuario.NombreCompleto.Length)).ToUpper();

            var hoy = DateTime.Today;
            DpDesde.SelectedDate = hoy.AddDays(-(int)hoy.DayOfWeek + (int)DayOfWeek.Monday);
            DpHasta.SelectedDate = hoy;
        }

        private void CargarEmpleados()
        {
            var empleados = new List<string> { "Todos los empleados" };

            try
            {
                using var conn = ConexionDB.ObtenerConexion();
                conn.Open();
                var cmd = new SqlCommand(
                    "SELECT nombre_completo FROM usuarios WHERE activo = 1 ORDER BY nombre_completo",
                    conn);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                    empleados.Add(r.GetString(0));
            }
            catch {}

            CmbEmpleado.SelectionChanged -= Filtros_Changed;
            CmbEmpleado.ItemsSource = empleados;
            CmbEmpleado.SelectedIndex = 0;
            CmbEmpleado.SelectionChanged += Filtros_Changed;
        }

        private (DateTime desde, DateTime hasta) ObtenerRango()
        {
            var hoy = DateTime.Today;
            return _periodo switch
            {
                "hoy" => (hoy, hoy),
                "mes" => (new DateTime(hoy.Year, hoy.Month, 1), hoy),
                "custom" => (DpDesde.SelectedDate ?? hoy.AddDays(-6),
                             DpHasta.SelectedDate ?? hoy),
                _ => 
                    (hoy.AddDays(-(int)hoy.DayOfWeek + (int)DayOfWeek.Monday), hoy)
            };
        }
        private void AplicarFiltros()
        {
            var (desde, hasta) = ObtenerRango();
            var empleadoFiltro = CmbEmpleado.SelectedIndex > 0
                ? CmbEmpleado.SelectedItem?.ToString()
                : null;

            _filas = ConsultarVentasPorDia(desde, hasta, empleadoFiltro);
            _pagActual = 1;

            ActualizarCards();
            ActualizarGrafico();
            ActualizarTabla();
            ActualizarSubtitulo(desde, hasta);
        }
        private List<FilaDia> ConsultarVentasPorDia(DateTime desde, DateTime hasta, string? empleado)
        {
            var filas = new List<FilaDia>();
            var ci = new CultureInfo("es-AR");

            try
            {
                using var conn = ConexionDB.ObtenerConexion();
                conn.Open();

                var sql = new StringBuilder(@"
                    SELECT
                        CAST(v.fecha_hora AS DATE)          AS dia,
                        COUNT(v.id)                         AS cant_ventas,
                        SUM(v.total)                        AS total_dia,
                        -- Empleado más activo del día (el que más vendió)
                        (
                            SELECT TOP 1 u2.nombre_completo
                            FROM ventas v2
                            INNER JOIN usuarios u2 ON u2.id = v2.usuario_id
                            WHERE CAST(v2.fecha_hora AS DATE) = CAST(v.fecha_hora AS DATE)");

                if (empleado != null)
                    sql.Append(" AND u2.nombre_completo = @emp");

                sql.Append(@"
                            GROUP BY u2.nombre_completo
                            ORDER BY COUNT(v2.id) DESC
                        )                                   AS empleado_top
                    FROM ventas v
                    INNER JOIN usuarios u ON u.id = v.usuario_id
                    WHERE CAST(v.fecha_hora AS DATE) BETWEEN @desde AND @hasta");

                if (empleado != null)
                    sql.Append(" AND u.nombre_completo = @emp");

                sql.Append(@"
                    GROUP BY CAST(v.fecha_hora AS DATE)
                    ORDER BY CAST(v.fecha_hora AS DATE)");

                var cmd = new SqlCommand(sql.ToString(), conn);
                cmd.Parameters.AddWithValue("@desde", desde);
                cmd.Parameters.AddWithValue("@hasta", hasta);
                if (empleado != null)
                    cmd.Parameters.AddWithValue("@emp", empleado);

                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    var fecha = r.GetDateTime(0);
                    var cantVentas = r.GetInt32(1);
                    var totalDia = r.GetDecimal(2);
                    var empTop = r.IsDBNull(3) ? "—" : r.GetString(3);
                    var ticket = cantVentas > 0 ? totalDia / cantVentas : 0m;
                    var diaNombre = fecha.ToString("dddd", ci);
                    diaNombre = char.ToUpper(diaNombre[0]) + diaNombre[1..];

                    filas.Add(new FilaDia
                    {
                        Fecha = fecha,
                        DiaNombre = diaNombre,
                        FechaDisplay = fecha.ToString("dd/MM/yyyy"),
                        Empleado = empTop,
                        CantidadVentas = cantVentas,
                        Total = totalDia,
                        TotalDisplay = totalDia.ToString("C0", ci),
                        TicketDisplay = ticket.ToString("C0", ci),
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar el reporte:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            if (filas.Count > 0)
            {
                var max = filas.Max(f => f.Total);
                foreach (var f in filas.Where(f => f.Total == max))
                    f.EsMejorDia = true;
            }

            return filas;
        }

        private void ActualizarCards()
        {
            var ci = new CultureInfo("es-AR");
            var totalIngresos = _filas.Sum(f => f.Total);
            var totalVentas = _filas.Sum(f => f.CantidadVentas);
            var ticketPromedio = totalVentas > 0 ? totalIngresos / totalVentas : 0m;
            var mejor = _filas.OrderByDescending(f => f.Total).FirstOrDefault();

            TxtIngresos.Text = totalIngresos.ToString("C0", ci);
            TxtTransacciones.Text = totalVentas.ToString();
            TxtTicket.Text = ticketPromedio.ToString("C0", ci);

            if (mejor != null)
            {
                TxtMejorDia.Text = mejor.DiaNombre;
                TxtMejorDiaSub.Text = $"{mejor.TotalDisplay} · {mejor.CantidadVentas} ventas";
            }
            else
            {
                TxtMejorDia.Text = "—";
                TxtMejorDiaSub.Text = "sin datos";
            }

            TxtPeriodoCard.Text = _periodo switch
            {
                "hoy" => "Hoy",
                "mes" => "Este mes",
                "custom" => "Período personalizado",
                _ => "Esta semana"
            };
        }

        private void ActualizarGrafico()
        {
            GraficoGrid.Children.Clear();
            GraficoGrid.ColumnDefinitions.Clear();
            GraficoLabels.Children.Clear();
            GraficoLabels.ColumnDefinitions.Clear();

            if (_filas.Count == 0) return;

            var maxTotal = _filas.Max(f => f.Total);
            if (maxTotal == 0) return;

            var hoy = DateTime.Today;
            var colorNormal = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C0DD97"));
            var colorActivo = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E7D32"));
            var colorLabel = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#AAAAAA"));

            for (int i = 0; i < _filas.Count; i++)
            {
                GraficoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                GraficoLabels.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var fila = _filas[i];
                var pct = (double)(fila.Total / maxTotal);
                var esHoy = fila.Fecha == hoy;
                var barH = Math.Max(4, pct * 60); 

                var col = new Grid();
                col.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                col.RowDefinitions.Add(new RowDefinition { Height = new GridLength(barH) });

                var txtVal = new TextBlock
                {
                    Text = $"{(int)(fila.Total / 1000)}k",
                    FontSize = 10,
                    Foreground = colorLabel,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(0, 0, 0, 2)
                };
                Grid.SetRow(txtVal, 0);
                col.Children.Add(txtVal);

                var rect = new Border
                {
                    Background = esHoy ? colorActivo : colorNormal,
                    CornerRadius = new CornerRadius(3, 3, 0, 0),
                    Margin = new Thickness(2, 0, 2, 0)
                };
                Grid.SetRow(rect, 1);
                col.Children.Add(rect);

                Grid.SetColumn(col, i);
                GraficoGrid.Children.Add(col);

                var diaCort = fila.DiaNombre.Length >= 3 ? fila.DiaNombre[..3] : fila.DiaNombre;
                var lbl = new TextBlock
                {
                    Text = diaCort,
                    FontSize = 10,
                    Foreground = colorLabel,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                Grid.SetColumn(lbl, i);
                GraficoLabels.Children.Add(lbl);
            }

            TxtGraficoTitulo.Text = _periodo switch
            {
                "hoy" => "Ingresos por hora — hoy",
                "mes" => "Ingresos por día — este mes",
                "custom" => "Ingresos por día — período seleccionado",
                _ => "Ingresos por día — semana actual"
            };
        }

        private void ActualizarTabla()
        {
            var total = _filas.Count;
            var paginas = (int)Math.Ceiling(total / (double)PageSize);
            if (_pagActual > paginas && paginas > 0) _pagActual = paginas;

            var pagina = _filas
                .Skip((_pagActual - 1) * PageSize)
                .Take(PageSize)
                .ToList();

            DgReporte.ItemsSource = pagina;

            var desde = total == 0 ? 0 : (_pagActual - 1) * PageSize + 1;
            var hasta = Math.Min(_pagActual * PageSize, total);
            TxtPaginaInfo.Text = $"Mostrando {desde}–{hasta} de {total} registros";
            TxtTablaCount.Text = $"{total} registro{(total != 1 ? "s" : "")} encontrado{(total != 1 ? "s" : "")}";

            PanelPaginacion.Children.Clear();
            if (paginas <= 1) return;

            void AddPagBtn(string label, int target, bool activo)
            {
                var btn = new Button
                {
                    Content = label,
                    Style = (Style)FindResource("PagBtn"),
                    Margin = new Thickness(2, 0, 0, 0),
                    IsEnabled = target >= 1 && target <= paginas,
                    Background = activo ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E7D32")) : Brushes.Transparent,
                    Foreground = activo ? Brushes.White : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#666666")),
                    BorderBrush = activo ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E7D32"))
                                             : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DDDDDD"))
                };
                btn.Click += (_, _) => { _pagActual = target; ActualizarTabla(); };
                PanelPaginacion.Children.Add(btn);
            }

            AddPagBtn("Ant.", _pagActual - 1, false);
            for (int p = 1; p <= paginas; p++)
                AddPagBtn(p.ToString(), p, p == _pagActual);
            AddPagBtn("Sig.", _pagActual + 1, false);
        }

        private void ActualizarSubtitulo(DateTime desde, DateTime hasta)
        {
            TxtSubtitulo.Text = desde == hasta
                ? $"Datos del {desde:dd/MM/yyyy}"
                : $"Del {desde:dd/MM/yyyy} al {hasta:dd/MM/yyyy} · {_filas.Sum(f => f.CantidadVentas)} operaciones";
        }

        private void BtnPeriodo_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
            _periodo = btn.Tag?.ToString() ?? "semana";

            BtnPeriodoHoy.Style = (Style)FindResource("PeriodBtn");
            BtnPeriodoSemana.Style = (Style)FindResource("PeriodBtn");
            BtnPeriodoMes.Style = (Style)FindResource("PeriodBtn");
            BtnPeriodoPersonalizado.Style = (Style)FindResource("PeriodBtn");
            btn.Style = (Style)FindResource("PeriodBtnActive");

            var esCustom = _periodo == "custom";
            DpDesde.IsEnabled = esCustom;
            DpHasta.IsEnabled = esCustom;

            AplicarFiltros();
        }

        private void Filtros_Changed(object sender, EventArgs e)
        {
            if (DpDesde.SelectedDate == null || DpHasta.SelectedDate == null) return;
            AplicarFiltros();
        }
        private void BtnVerDia_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
            if (btn.Tag is not DateTime fecha) return;
            try
            {
                var ci = new CultureInfo("es-AR");
                var lineas = new StringBuilder();
                lineas.AppendLine($"Ventas del {fecha:dd/MM/yyyy}");
                lineas.AppendLine(new string('─', 50));

                using var conn = ConexionDB.ObtenerConexion();
                conn.Open();
                var cmd = new SqlCommand(@"
                    SELECT v.numero_comprobante,
                           v.fecha_hora,
                           u.nombre_completo,
                           v.metodo_pago,
                           v.total
                    FROM ventas v
                    INNER JOIN usuarios u ON u.id = v.usuario_id
                    WHERE CAST(v.fecha_hora AS DATE) = @fecha
                    ORDER BY v.fecha_hora", conn);
                cmd.Parameters.AddWithValue("@fecha", fecha);

                using var r = cmd.ExecuteReader();
                int count = 0;
                while (r.Read())
                {
                    count++;
                    var nro = r.GetString(0);
                    var hora = r.GetDateTime(1).ToString("HH:mm");
                    var emp = r.GetString(2);
                    var metodo = r.GetString(3);
                    var total = r.GetDecimal(4).ToString("C0", ci);
                    lineas.AppendLine($"{nro}  {hora}  {emp}  [{metodo}]  {total}");
                }

                if (count == 0)
                    lineas.AppendLine("Sin ventas registradas.");

                MessageBox.Show(lineas.ToString(), $"Detalle — {fecha:dd/MM/yyyy}",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar el detalle:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnExportar_Click(object sender, RoutedEventArgs e)
        {
            if (_filas.Count == 0)
            {
                MessageBox.Show("No hay datos para exportar.", "Exportar PDF",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var carpetaReportes = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "Reportes");

                Directory.CreateDirectory(carpetaReportes);
                var (desde, hasta) = ObtenerRango();
                var ruta = Path.Combine(
                    carpetaReportes,
                    $"reporte_ventas_{desde:yyyyMMdd}_{hasta:yyyyMMdd}.pdf");

                var ci = new CultureInfo("es-AR");

                decimal efectivo = 0, tarjeta = 0, transferencia = 0;

                try
                {
                    using var conn = ConexionDB.ObtenerConexion();
                    conn.Open();

                    var cmd = new SqlCommand(@"
                SELECT metodo_pago, ISNULL(SUM(total), 0)
                FROM ventas
                WHERE CAST(fecha_hora AS DATE) BETWEEN @desde AND @hasta
                GROUP BY metodo_pago", conn);

                    cmd.Parameters.AddWithValue("@desde", desde);
                    cmd.Parameters.AddWithValue("@hasta", hasta);

                    using var r = cmd.ExecuteReader();
                    while (r.Read())
                    {
                        var metodo = r.GetString(0);
                        var monto = r.GetDecimal(1);

                        switch (metodo)
                        {
                            case "efectivo": efectivo += monto; break;
                            case "tarjeta": tarjeta += monto; break;
                            case "transferencia": transferencia += monto; break;
                        }
                    }
                }
                catch { }

                var mejor = _filas.OrderByDescending(f => f.Total).FirstOrDefault();

                var data = new ReporteVentasData
                {
                    Administrador = _usuario.NombreCompleto,
                    FechaDesde = desde,
                    FechaHasta = hasta,
                    FechaGeneracion = DateTime.Now,
                    TotalIngresos = _filas.Sum(f => f.Total),
                    TotalTransacciones = _filas.Sum(f => f.CantidadVentas),
                    TicketPromedio = _filas.Sum(f => f.CantidadVentas) > 0
                        ? _filas.Sum(f => f.Total) / _filas.Sum(f => f.CantidadVentas)
                        : 0m,
                    MejorDia = mejor?.DiaNombre ?? "—",
                    TotalEfectivo = efectivo,
                    TotalTarjeta = tarjeta,
                    TotalTransferencia = transferencia,
                    Filas = _filas.Select(f => new Reports.FilaDiaDto
                    {
                        DiaNombre = f.DiaNombre,
                        Fecha = f.Fecha,
                        CantidadVentas = f.CantidadVentas,
                        Total = f.Total,
                        TicketPromedio = f.CantidadVentas > 0 ? f.Total / f.CantidadVentas : 0m,
                        Empleado = f.Empleado,
                        EsMejorDia = f.EsMejorDia,
                    }).ToList()
                };

                Reports.ReporteVentasPdf.Generate(ruta, data);

                var res = MessageBox.Show(
                    $"PDF exportado correctamente.\n{ruta}\n\n¿Deseas abrirlo ahora?",
                    "Exportar PDF",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (res == MessageBoxResult.Yes)
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = ruta,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al exportar el PDF:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private void BtnInventario_Click(object sender, RoutedEventArgs e)
        {
            var w = new MenuDuenoWindow(_usuario);
            w.Show();
            Close();
        }

        private void BtnAgregarMaterial_Click(object sender, RoutedEventArgs e)
        {
            var w = new ElApostol_WPF.UI.Dueño.AgregarEditarProductoWindow();
            w.Show();
        }

        private void BtnNuevaVenta_Click(object sender, RoutedEventArgs e)
        {
            var ventana = new ElApostol_WPF.UI.Shared.NuevaVentaWindow(_usuario);
            this.Hide();

            ventana.Closed += (s, args) =>
            {
                this.Show();
            };

            ventana.ShowDialog();
        }

        private void BtnGestionEmpleados_Click(object sender, RoutedEventArgs e)
        {
            var w = new ElApostol_WPF.UI.Dueño.GestionEmpleadosWindow(_usuario);
            w.Show();
            Close();
        }

        private void BtnCambiarClaveEmpleado_Click(object sender, RoutedEventArgs e)
        {
            var w = new ElApostol_WPF.UI.Dueño.CambiarContrasenaEmpleadoWindow(_usuario);
            w.Show();
        }

        private void BtnCerrarSesion_Click(object sender, RoutedEventArgs e)
        {
            var res = MessageBox.Show("¿Cerrar sesión?", "El Apóstol",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res != MessageBoxResult.Yes) return;

            var login = new ElApostol_WPF.UI.Login.LoginWindow();
            login.Show();
            Close();
        }
        
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.F1: BtnInventario_Click(sender, e); break;
                case Key.F2: BtnAgregarMaterial_Click(sender, e); break;
                case Key.F3: BtnNuevaVenta_Click(sender, e); break;
                case Key.F5: BtnGestionEmpleados_Click(sender, e); break;
                case Key.F6: BtnCambiarClaveEmpleado_Click(sender, e); break;
                case Key.F12: BtnCerrarSesion_Click(sender, e); break;
            }
        }
    }
}