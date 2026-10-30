using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ElApostol_WPF.Data;
using ElApostol_WPF.Models;
using Microsoft.Data.SqlClient;
using ElApostol_WPF.Reports;

namespace ElApostol_WPF.UI.Dueño
{
    public partial class ReporteVentasControl : UserControl
    {
        private Usuario _usuario = null!;

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

        public ReporteVentasControl()
        {
            InitializeComponent();
        }

        public void Inicializar(Usuario usuario)
        {
            _usuario = usuario;
            InicializarUI();
            CargarEmpleados();
            AplicarFiltros();
        }

        private void InicializarUI()
        {
            DpDesde.SelectedDate = LunesDeEstaSemana();
            DpHasta.SelectedDate = DateTime.Today;
        }

        // El domingo pertenece a la semana que empezó el lunes anterior
        private static DateTime LunesDeEstaSemana() =>
            DateTime.Today.AddDays(-(((int)DateTime.Today.DayOfWeek + 6) % 7));

        // Ids paralelos a los ítems del combo (índice 0 = "Todos")
        private readonly List<int?> _empleadoIds = new();

        private int? EmpleadoSeleccionadoId =>
            CmbEmpleado.SelectedIndex > 0 && CmbEmpleado.SelectedIndex < _empleadoIds.Count
                ? _empleadoIds[CmbEmpleado.SelectedIndex]
                : null;

        private void CargarEmpleados()
        {
            var empleados = new List<string> { "Todos los empleados" };
            _empleadoIds.Clear();
            _empleadoIds.Add(null);

            try
            {
                using var conn = ConexionDB.ObtenerConexion();
                conn.Open();
                // Se incluyen inactivos para poder consultar ventas de ex-empleados
                var cmd = new SqlCommand(
                    "SELECT id, nombre_completo, usuario, activo FROM usuarios ORDER BY activo DESC, nombre_completo",
                    conn);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    _empleadoIds.Add(r.GetInt32(0));
                    var inactivo = r.GetBoolean(3) ? "" : " · inactivo";
                    empleados.Add($"{r.GetString(1)} (@{r.GetString(2)}){inactivo}");
                }
            }
            catch { }

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
                _ => (LunesDeEstaSemana(), hoy)
            };
        }
        private void AplicarFiltros()
        {
            var (desde, hasta) = ObtenerRango();
            var empleadoFiltro = EmpleadoSeleccionadoId;

            _filas = ConsultarVentasPorDia(desde, hasta, empleadoFiltro);
            _pagActual = 1;

            ActualizarCards();
            ActualizarGrafico();
            ActualizarTabla();
            ActualizarSubtitulo(desde, hasta);
        }
        private List<FilaDia> ConsultarVentasPorDia(DateTime desde, DateTime hasta, int? empleado)
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
                    sql.Append(" AND v2.usuario_id = @emp");

                sql.Append(@"
                            GROUP BY u2.nombre_completo
                            ORDER BY COUNT(v2.id) DESC
                        )                                   AS empleado_top
                    FROM ventas v
                    INNER JOIN usuarios u ON u.id = v.usuario_id
                    WHERE CAST(v.fecha_hora AS DATE) BETWEEN @desde AND @hasta");

                if (empleado != null)
                    sql.Append(" AND v.usuario_id = @emp");

                sql.Append(@"
                    GROUP BY CAST(v.fecha_hora AS DATE)
                    ORDER BY CAST(v.fecha_hora AS DATE)");

                var cmd = new SqlCommand(sql.ToString(), conn);
                cmd.Parameters.AddWithValue("@desde", desde);
                cmd.Parameters.AddWithValue("@hasta", hasta);
                if (empleado != null)
                    cmd.Parameters.AddWithValue("@emp", empleado.Value);

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
                "hoy" => "Ingresos — hoy",
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

                var empleadoId = EmpleadoSeleccionadoId;

                using var conn = ConexionDB.ObtenerConexion();
                conn.Open();
                var cmd = new SqlCommand($@"
                    SELECT v.id,
                           v.numero_comprobante,
                           v.fecha_hora,
                           u.nombre_completo,
                           v.metodo_pago,
                           v.total,
                           p.nombre,
                           vp.medida,
                           dv.cantidad,
                           dv.subtotal_linea
                    FROM ventas v
                    INNER JOIN usuarios u ON u.id = v.usuario_id
                    LEFT JOIN detalle_ventas     dv ON dv.venta_id = v.id
                    LEFT JOIN variantes_producto vp ON vp.id = dv.variante_id
                    LEFT JOIN productos          p  ON p.id  = vp.producto_id
                    WHERE CAST(v.fecha_hora AS DATE) = @fecha
                    {(empleadoId != null ? "AND v.usuario_id = @emp" : "")}
                    ORDER BY v.fecha_hora, v.id, dv.id", conn);
                cmd.Parameters.AddWithValue("@fecha", fecha);
                if (empleadoId != null)
                    cmd.Parameters.AddWithValue("@emp", empleadoId.Value);

                using var r = cmd.ExecuteReader();
                int count = 0;
                int ventaActual = -1;
                while (r.Read())
                {
                    var ventaId = r.GetInt32(0);
                    if (ventaId != ventaActual)
                    {
                        if (count > 0) lineas.AppendLine();
                        count++;
                        ventaActual = ventaId;

                        var nro = r.GetString(1);
                        var hora = r.GetDateTime(2).ToString("HH:mm");
                        var emp = r.GetString(3);
                        var metodo = r.GetString(4);
                        var total = r.GetDecimal(5).ToString("C0", ci);
                        lineas.AppendLine($"{nro}  {hora}  {emp}  [{metodo}]  {total}");
                    }

                    if (!r.IsDBNull(6))
                    {
                        var medida = r.IsDBNull(7) ? "" : $" ({r.GetString(7)})";
                        var cant = r.GetInt32(8);
                        var sub = r.GetDecimal(9).ToString("C0", ci);
                        lineas.AppendLine($"    {cant} × {r.GetString(6)}{medida}  —  {sub}");
                    }
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
                var empleadoFiltro = EmpleadoSeleccionadoId;

                try
                {
                    using var conn = ConexionDB.ObtenerConexion();
                    conn.Open();

                    var cmd = new SqlCommand($@"
                SELECT metodo_pago, ISNULL(SUM(total), 0)
                FROM ventas
                WHERE CAST(fecha_hora AS DATE) BETWEEN @desde AND @hasta
                {(empleadoFiltro != null ? "AND usuario_id = @emp" : "")}
                GROUP BY metodo_pago", conn);

                    cmd.Parameters.AddWithValue("@desde", desde);
                    cmd.Parameters.AddWithValue("@hasta", hasta);
                    if (empleadoFiltro != null)
                        cmd.Parameters.AddWithValue("@emp", empleadoFiltro.Value);

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

                var ventasDetalle = new List<Reports.VentaDetalleDto>();

                try
                {
                    using var conn = ConexionDB.ObtenerConexion();
                    conn.Open();

                    var sqlDetalle = new StringBuilder(@"
                        SELECT v.numero_comprobante, v.fecha_hora, u.nombre_completo, v.metodo_pago, v.total
                        FROM ventas v
                        INNER JOIN usuarios u ON u.id = v.usuario_id
                        WHERE CAST(v.fecha_hora AS DATE) BETWEEN @desde AND @hasta");

                    if (empleadoFiltro != null)
                        sqlDetalle.Append(" AND v.usuario_id = @emp");

                    sqlDetalle.Append(" ORDER BY v.fecha_hora DESC");

                    var cmdDetalle = new SqlCommand(sqlDetalle.ToString(), conn);
                    cmdDetalle.Parameters.AddWithValue("@desde", desde);
                    cmdDetalle.Parameters.AddWithValue("@hasta", hasta);
                    if (empleadoFiltro != null)
                        cmdDetalle.Parameters.AddWithValue("@emp", empleadoFiltro.Value);

                    using var rDetalle = cmdDetalle.ExecuteReader();
                    while (rDetalle.Read())
                    {
                        ventasDetalle.Add(new Reports.VentaDetalleDto
                        {
                            NumeroComprobante = rDetalle.GetString(0),
                            FechaHora = rDetalle.GetDateTime(1),
                            Empleado = rDetalle.GetString(2),
                            MetodoPago = rDetalle.GetString(3),
                            Total = rDetalle.GetDecimal(4),
                        });
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
                    }).ToList(),
                    Ventas = ventasDetalle
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
    }
}
