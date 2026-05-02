    using ElApostol_WPF.Data;
    using ElApostol_WPF.Models;
    using System.Collections.ObjectModel;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Windows.Media;
    using System.Windows.Shapes;

    namespace ElApostol_WPF.UI.Shared
    {
        public partial class NuevaVentaWindow : Window
        {
            private readonly Usuario _usuario;
            private readonly VentaRepository _repo = new();
            private readonly ObservableCollection<CarritoItem> _carrito = new();
            private Producto? _productoSeleccionado;
            private VarianteProducto? _varianteSeleccionada;
            private string? _metodoPago;
            private int? _varianteInicialId;
            private readonly ProductoRepository _productoRepo = new();

            public NuevaVentaWindow(Usuario usuario)
            {
                InitializeComponent();
                _usuario = usuario;

                TxtHeaderUsuario.Text = $"{usuario.NombreCompleto} · Turno activo";
                DgCarrito.ItemsSource = _carrito;

                TxtBadgeTarjeta.Text = $"+{VentaRepository.RecargoTarjeta * 100:0}%";
                TxtLabelRecargo.Text = $"Recargo tarjeta ({VentaRepository.RecargoTarjeta * 100:0}%)";

                _carrito.CollectionChanged += (_, _) => ActualizarTotales();
            }

            public NuevaVentaWindow(Usuario usuario, int? varianteId) : this(usuario)
            {
                _varianteInicialId = varianteId;

                if (_varianteInicialId != null)
                    CargarVarianteInicial(_varianteInicialId.Value);
            }

            private void CargarVarianteInicial(int varianteId)
            {
                try
                {
                    var producto = _productoRepo.ObtenerProductoPorVariante(varianteId);
                    if (producto == null) return;

                    var variante = producto.Variantes.FirstOrDefault(v => v.Id == varianteId);
                    if (variante == null) return;

                    SeleccionarProductoVariante(producto, variante);
                }
                catch { }
            }

            private void TxtBusqueda_TextChanged(object sender, TextChangedEventArgs e)
            {
                var texto = TxtBusqueda.Text.Trim();

                if (texto.Length < 2)
                {
                    OcultarSugerencias();
                    LimpiarSeleccionProducto();
                    return;
                }

                var resultados = _repo.BuscarParaVenta(texto);

                if (!resultados.Any())
                {
                    OcultarSugerencias();
                    return;
                }

                MostrarSugerencias(resultados);
            }

            private void TxtBusqueda_KeyDown(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Escape)
                {
                    OcultarSugerencias();
                    LimpiarSeleccionProducto();
                }
            }

            private void MostrarSugerencias(List<Producto> productos)
            {
                ListaSugerencias.Children.Clear();

                foreach (var prod in productos)
                {
                    foreach (var v in prod.Variantes)
                    {
                        var item = CrearFilaSugerencia(prod, v);
                        ListaSugerencias.Children.Add(item);
                    }
                }

                PanelSugerencias.Visibility = Visibility.Visible;
            }

        private Border CrearFilaSugerencia(Producto prod, VarianteProducto v)
        {
            var fila = new Border
            {
                Padding = new Thickness(12, 8, 12, 8),
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F0F0F0")),
                BorderThickness = new Thickness(0, 0, 0, 0.5)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var izq = new StackPanel();

            var nombre = new TextBlock
            {
                Text = $"{prod.Nombre}  ·  {v.Medida}",
                FontSize = 13,
                FontWeight = FontWeights.Medium,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A1A1A"))
            };
            var detalle = new TextBlock
            {
                Text = $"{prod.Marca ?? ""}  ·  {v.Calidad ?? ""}",
                FontSize = 12,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#666666")),
                Margin = new Thickness(0, 2, 0, 0)
            };
            izq.Children.Add(nombre);
            izq.Children.Add(detalle);
            Grid.SetColumn(izq, 0);

            var der = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 0, 0)
            };

            var precio = new TextBlock
            {
                Text = $"${v.PrecioUnitario:N0}",
                FontSize = 13,
                FontWeight = FontWeights.Medium,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1B5E20")),
                HorizontalAlignment = HorizontalAlignment.Right
            };

            bool stockBajo = v.StockActual <= 5;
            var badge = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(stockBajo ? "#FAEEDA" : "#EAF3DE")),
                CornerRadius = new CornerRadius(20),
                Padding = new Thickness(7, 1, 7, 1),
                Margin = new Thickness(0, 3, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            badge.Child = new TextBlock
            {
                Text = $"Stock: {v.StockActual}",
                FontSize = 11,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(stockBajo ? "#633806" : "#27500A"))
            };

            der.Children.Add(precio);
            der.Children.Add(badge);
            Grid.SetColumn(der, 1);

            grid.Children.Add(izq);
            grid.Children.Add(der);
            fila.Child = grid;

            fila.MouseEnter += (_, _) =>
                fila.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EAF3DE"));
            fila.MouseLeave += (_, _) =>
                fila.Background = Brushes.Transparent;

            fila.MouseLeftButtonDown += (_, _) => SeleccionarProductoVariante(prod, v);
            return fila;
        }

        private void SeleccionarProductoVariante(Producto prod, VarianteProducto varianteDirecta)
            {
                _productoSeleccionado = prod;
                TxtBusqueda.Text = prod.Nombre;
                OcultarSugerencias();

                CmbVariante.Items.Clear();
                int selIdx = 0;
                for (int i = 0; i < prod.Variantes.Count; i++)
                {
                    var v = prod.Variantes[i];
                    CmbVariante.Items.Add(new ComboBoxItem
                    {
                        Content = $"{v.Medida} · ${v.PrecioUnitario:N0}",
                        Tag = v
                    });
                    if (v.Id == varianteDirecta.Id) selIdx = i;
                }

                CmbVariante.SelectedIndex = selIdx;
                _varianteSeleccionada = varianteDirecta;
                BtnAgregar.IsEnabled = true;
            }

            private void OcultarSugerencias() =>
                PanelSugerencias.Visibility = Visibility.Collapsed;

            private void LimpiarSeleccionProducto()
            {
                _productoSeleccionado = null;
                _varianteSeleccionada = null;
                CmbVariante.Items.Clear();
                BtnAgregar.IsEnabled = false;
            }

            private void CmbVariante_SelectionChanged(object sender, SelectionChangedEventArgs e)
            {
                if (CmbVariante.SelectedItem is ComboBoxItem item && item.Tag is VarianteProducto v)
                {
                    _varianteSeleccionada = v;
                    BtnAgregar.IsEnabled = true;
                }
            }

            private void BtnMenos_Click(object sender, RoutedEventArgs e)
            {
                if (int.TryParse(TxtCantidad.Text, out int cant) && cant > 1)
                    TxtCantidad.Text = (cant - 1).ToString();
            }

            private void BtnMas_Click(object sender, RoutedEventArgs e)
            {
                if (int.TryParse(TxtCantidad.Text, out int cant))
                    TxtCantidad.Text = (cant + 1).ToString();
            }

            private void TxtCantidad_PreviewTextInput(object sender, TextCompositionEventArgs e)
            {
                e.Handled = !e.Text.All(char.IsDigit);
            }

            private void BtnAgregar_Click(object sender, RoutedEventArgs e)
            {
                if (_productoSeleccionado == null || _varianteSeleccionada == null) return;

                if (!int.TryParse(TxtCantidad.Text, out int cant) || cant < 1) cant = 1;

                var existente = _carrito.FirstOrDefault(c => c.VarianteId == _varianteSeleccionada.Id);
                if (existente != null)
                {
                    var idx = _carrito.IndexOf(existente);
                    _carrito[idx] = existente with { Cantidad = existente.Cantidad + cant };
                }
                else
                {
                    _carrito.Add(new CarritoItem(
                        _varianteSeleccionada.Id,
                        _productoSeleccionado.CodigoInterno,
                        _productoSeleccionado.Nombre,
                        _productoSeleccionado.Marca ?? "-",
                        _varianteSeleccionada.Medida ?? "-",
                        cant,
                        _varianteSeleccionada.PrecioUnitario
                    ));
                }

                TxtCantidad.Text = "1";
                ActualizarInfoCarrito();
            }

            private void BtnEliminarItem_Click(object sender, RoutedEventArgs e)
            {
                if ((sender as Button)?.Tag is int varianteId)
                {
                    var item = _carrito.FirstOrDefault(c => c.VarianteId == varianteId);
                    if (item != null) _carrito.Remove(item);
                    ActualizarInfoCarrito();
                }
            }

            private void PayCard_Click(object sender, MouseButtonEventArgs e)
            {
                if (sender is not Border card) return;

                _metodoPago = card.Tag?.ToString();

                DesmarcarCard(CardEfectivo, RadioEfectivo);
                DesmarcarCard(CardTarjeta, RadioTarjeta);
                DesmarcarCard(CardTransferencia, RadioTransferencia);

                MarcarCard(card, card == CardEfectivo ? RadioEfectivo
                               : card == CardTarjeta ? RadioTarjeta
                               : RadioTransferencia);

                FilaRecargo.Visibility = _metodoPago == "tarjeta"
                    ? Visibility.Visible
                    : Visibility.Collapsed;

                ActualizarTotales();
                VerificarFinalizar();
            }

            private static void MarcarCard(Border card, Ellipse radio)
            {
                card.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E7D32"));
                card.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EAF3DE"));
                radio.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E7D32"));
                radio.Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E7D32"));
            }

            private static void DesmarcarCard(Border card, Ellipse radio)
            {
                card.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DDDDDD"));
                card.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F5F5F5"));
                radio.Fill = Brushes.Transparent;
                radio.Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CCCCCC"));
            }

            private void ActualizarTotales()
            {
                var subtotal = _carrito.Sum(c => c.SubtotalLinea);
                TxtSubtotal.Text = $"${subtotal:N0}";

                decimal recargo = 0;
                if (_metodoPago == "tarjeta")
                    recargo = subtotal * VentaRepository.RecargoTarjeta;

                TxtRecargo.Text = $"${recargo:N0}";
                TxtTotal.Text = $"${subtotal + recargo:N0}";

                TxtMetodoPagoInfo.Text = _metodoPago switch
                {
                    "efectivo" => "Pago en efectivo — sin recargo",
                    "tarjeta" => $"Tarjeta — recargo {VentaRepository.RecargoTarjeta * 100:0}% incluido",
                    "transferencia" => "Transferencia bancaria — sin recargo",
                    _ => "— sin método seleccionado —"
                };

                VerificarFinalizar();
            }

            private void ActualizarInfoCarrito()
            {
                var total = _carrito.Sum(c => c.Cantidad);
                TxtInfoCarrito.Text = total == 0
                    ? "Sin ítems"
                    : $"{_carrito.Count} línea{(_carrito.Count != 1 ? "s" : "")} · {total} unidad{(total != 1 ? "es" : "")} en total";
            }

            private void VerificarFinalizar()
            {
                BtnFinalizar.IsEnabled = _carrito.Any() && !string.IsNullOrEmpty(_metodoPago);
            }

        private void BtnFinalizar_Click(object sender, RoutedEventArgs e)
        {
            if (!_carrito.Any() || string.IsNullOrEmpty(_metodoPago)) return;

            try
            {
                var subtotal = _carrito.Sum(c => c.SubtotalLinea);
                decimal recargo = _metodoPago == "tarjeta"
                    ? subtotal * VentaRepository.RecargoTarjeta
                    : 0m;

                var venta = new Venta
                {
                    UsuarioId = _usuario.Id,
                    MetodoPago = _metodoPago!,
                    Subtotal = subtotal,
                    MontoRecargo = recargo,
                    Total = subtotal + recargo,
                    Observaciones = TxtObservaciones.Text.Trim(),
                    Detalles = _carrito.Select(c => new DetalleVenta
                    {
                        VarianteId = c.VarianteId,
                        Cantidad = c.Cantidad,
                        PrecioUnitarioHistorico = c.PrecioUnitarioHistorico,
                        SubtotalLinea = c.SubtotalLinea
                    }).ToList()
                };

                venta.NumeroComprobante = _repo.GenerarNumeroComprobante();
                venta.FechaHora = DateTime.Now;
                var ventaId = _repo.GuardarVenta(venta);
                venta.Id = ventaId;

                var confirmacion = new VentaConfirmacionWindow(
                    venta,
                    _usuario.NombreCompleto,
                    _carrito.ToList(),
                    VentaRepository.RecargoTarjeta
                );
                confirmacion.Owner = this;
                confirmacion.ShowDialog();

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al registrar la venta:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e)
            {
                if (_carrito.Any())
                {
                    var res = MessageBox.Show(
                        "¿Cancelar la venta? Se perderán los ítems cargados.",
                        "Cancelar venta",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (res != MessageBoxResult.Yes) return;
                }

                DialogResult = false;
                Close();
            }

            private void Window_KeyDown(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Escape)
                {
                    BtnCancelar_Click(sender, e);
                    e.Handled = true;
                }
            }
        }

        public record CarritoItem(
            int VarianteId,
            string CodigoInterno,
            string NombreProducto,
            string Marca,
            string Medida,
            int Cantidad,
            decimal PrecioUnitarioHistorico)
        {
            public decimal SubtotalLinea => Cantidad * PrecioUnitarioHistorico;
            public string PrecioDisplay => $"${PrecioUnitarioHistorico:N0}";
            public string SubtotalDisplay => $"${SubtotalLinea:N0}";
        }
    }