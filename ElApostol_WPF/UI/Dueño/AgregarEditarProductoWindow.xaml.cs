using System.Collections.ObjectModel;
using ElApostol_WPF.UI.Shared;
using System.Windows;
using System.Windows.Controls;
using ElApostol_WPF.Data;
using ElApostol_WPF.Models;

namespace ElApostol_WPF.UI.Dueño
{
    public partial class AgregarEditarProductoWindow : Window
    {
        private readonly ProductoRepository _repo = new();
        private readonly bool _modoEdicion;
        private readonly Producto? _productoOriginal;
        private readonly ObservableCollection<VarianteProducto> _variantes = new();
        private VarianteProducto? _varianteEnEdicion = null;

        public AgregarEditarProductoWindow()
        {
            InitializeComponent();
            _modoEdicion = false;
            InicializarVentana();
        }

        public AgregarEditarProductoWindow(Producto productoExistente)
        {
            InitializeComponent();
            _modoEdicion = true;
            _productoOriginal = productoExistente;
            InicializarVentana();
            CargarDatosProducto(productoExistente);
        }

        private void InicializarVentana()
        {
            ListVariantes.ItemsSource = _variantes;
            PrevVariantes.ItemsSource = _variantes;

            var categorias = _repo.ObtenerCategorias();
            CmbCategoria.ItemsSource = categorias;

            if (_modoEdicion)
            {
                Title = "Editar material";
                TxtTituloVentana.Text = "Editar material";
                TxtBadge.Text = "Modificando registro";
                BtnGuardar.Content = "Guardar cambios";
                TxtFooterHint.Text = "Modificá los campos y guardá los cambios";
            }
        }

        private void CargarDatosProducto(Producto p)
        {
            TxtNombre.Text = p.Nombre;
            TxtMarca.Text = p.Marca ?? string.Empty;
            TxtProveedor.Text = p.Proveedor ?? string.Empty;

            // El número del código: lo que sigue a "PRE-" (si el código tiene otro formato, queda vacío)
            var guion = p.CodigoInterno.LastIndexOf('-');
            var resto = guion >= 0 ? p.CodigoInterno[(guion + 1)..] : "";
            _numeroOriginal = resto.Length > 0 && resto.All(char.IsDigit) ? resto : "";

            _cargandoCodigo = true;
            TxtCodigoNumero.Text = _numeroOriginal;
            _cargandoCodigo = false;

            if (p.CategoriaId > 0)
            {
                var categorias = CmbCategoria.ItemsSource as List<Categoria>;
                CmbCategoria.SelectedItem = categorias?.FirstOrDefault(c => c.Id == p.CategoriaId);
            }

            foreach (var v in p.Variantes)
                _variantes.Add(v);

            ActualizarEstadoTablaVariantes();
            ActualizarPreview();
        }

        private void Campo_Changed(object sender, EventArgs e) => ActualizarPreview();

        // ── Código: prefijo (de la categoría) + número ──

        private string _numeroOriginal = "";
        private string? _numeroSugerido;
        private bool _cargandoCodigo;

        private string PrefijoActual => (CmbCategoria.SelectedItem as Categoria)?.Prefijo ?? "";

        /// <summary>Código completo armado con el prefijo y el número (ej. CON-015), o "" si falta algo.</summary>
        private string CodigoActual
        {
            get
            {
                var numero = TxtCodigoNumero.Text.Trim();
                if (PrefijoActual.Length == 0 || numero.Length == 0) return "";

                // En edición, si no se tocó nada, se respeta el código original tal cual
                if (_modoEdicion && _productoOriginal != null
                    && (CmbCategoria.SelectedItem as Categoria)?.Id == _productoOriginal.CategoriaId
                    && numero == _numeroOriginal)
                    return _productoOriginal.CodigoInterno;

                return int.TryParse(numero, out var n) ? $"{PrefijoActual}-{n:D3}" : "";
            }
        }

        private void CmbCategoria_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbCategoria.SelectedItem is Categoria cat)
            {
                TxtPrefijo.Text = $"{cat.Prefijo}-";
                try
                {
                    var siguiente = _repo.GenerarSiguienteCodigo(cat.Prefijo);
                    var numeroSiguiente = siguiente[(cat.Prefijo.Length + 1)..];
                    var actual = TxtCodigoNumero.Text.Trim();

                    if (_modoEdicion && cat.Id == _productoOriginal?.CategoriaId)
                    {
                        // Volvió a su categoría original: recupera su número
                        TxtCodigoNumero.Text = _numeroOriginal;
                    }
                    else if (actual.Length == 0 || actual == _numeroSugerido || _modoEdicion)
                    {
                        // Alta (o cambio de categoría en edición): proponemos el próximo libre
                        TxtCodigoNumero.Text = numeroSiguiente;
                    }
                    _numeroSugerido = numeroSiguiente;
                }
                catch { _numeroSugerido = null; }
            }
            else
            {
                TxtPrefijo.Text = "···-";
            }

            ActualizarHintCodigo();
            ActualizarPreview();
        }

        private void TxtCodigoNumero_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            e.Handled = !e.Text.All(char.IsDigit);
        }

        private void TxtCodigoNumero_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            var texto = e.DataObject.GetData(typeof(string)) as string ?? "";
            if (!texto.All(char.IsDigit)) e.CancelCommand();
        }

        private void TxtCodigoNumero_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_cargandoCodigo) return;
            ActualizarHintCodigo();
            ActualizarPreview();
        }

        /// <summary>
        /// Muestra debajo del número cuál fue el último código usado con ese prefijo
        /// y avisa en el momento si el número elegido ya está ocupado.
        /// </summary>
        private void ActualizarHintCodigo()
        {
            if (TxtCodigoHint == null) return;
            TxtErrorCodigo.Visibility = Visibility.Collapsed;

            var prefijo = PrefijoActual;
            if (prefijo.Length == 0)
            {
                TxtCodigoHint.Text = "Elegí una categoría para ver el último código usado.";
                TxtCodigoHint.Foreground = System.Windows.Media.Brushes.Gray;
                return;
            }

            string? ultimo = null;
            try { ultimo = _repo.ObtenerUltimoCodigo(prefijo); } catch { }

            TxtCodigoHint.Text = ultimo == null
                ? $"Sin códigos {prefijo} todavía · Próximo: {prefijo}-{_numeroSugerido ?? "001"}"
                : $"Último: {ultimo}  ·  Próximo libre: {prefijo}-{_numeroSugerido}";
            TxtCodigoHint.Foreground = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#27500A"));

            // Aviso inmediato si el número ya lo usa otro material
            var codigo = CodigoActual;
            if (codigo.Length == 0) return;
            try
            {
                var existente = _repo.ObtenerPorCodigo(codigo);
                if (existente != null && existente.Value.id != (_productoOriginal?.Id ?? 0))
                {
                    TxtErrorCodigo.Text = existente.Value.activo
                        ? $"✕ {codigo} ya lo usa \"{existente.Value.nombre}\"."
                        : $"✕ {codigo} ya lo usa \"{existente.Value.nombre}\" (desactivado).";
                    TxtErrorCodigo.Visibility = Visibility.Visible;
                }
            }
            catch { }
        }

        private void BtnGestionarCategorias_Click(object sender, RoutedEventArgs e)
        {
            var ventana = new GestionCategoriasWindow { Owner = this };
            ventana.ShowDialog();
            if (!ventana.HuboCambios) return;

            var seleccionId = (CmbCategoria.SelectedItem as Categoria)?.Id;
            var categorias = _repo.ObtenerCategorias();
            CmbCategoria.ItemsSource = categorias;
            CmbCategoria.SelectedItem = categorias.FirstOrDefault(c => c.Id == seleccionId);
        }

        /// <summary>
        /// Interpreta precios escritos al estilo argentino ("1.500", "1.500,50", "1500,5")
        /// y también con punto decimal ("1500.50").
        /// </summary>
        private static bool TryParsePrecio(string texto, out decimal precio)
        {
            texto = texto.Replace("$", "").Replace(" ", "");
            var ar = new System.Globalization.CultureInfo("es-AR");
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            var estilo = System.Globalization.NumberStyles.Number;

            if (texto.Contains(','))
                return decimal.TryParse(texto, estilo, ar, out precio);

            // Solo puntos: si tiene forma de miles (1.500 / 12.345.678) son separadores de miles
            if (System.Text.RegularExpressions.Regex.IsMatch(texto, @"^\d{1,3}(\.\d{3})+$"))
                return decimal.TryParse(texto.Replace(".", ""), estilo, inv, out precio);

            return decimal.TryParse(texto, estilo, inv, out precio);
        }

        private void ActualizarPreview()
        {
            var codigoPrev = CodigoActual;
            PrevCodigo.Text = codigoPrev.Length == 0 ? "— sin código —" : codigoPrev;
            PrevCodigo.Foreground = codigoPrev.Length == 0
                ? System.Windows.Media.Brushes.LightGray
                : System.Windows.Media.Brushes.Gray;

            if (string.IsNullOrWhiteSpace(TxtNombre.Text))
            {
                PrevNombre.Text = "Nombre del material";
                PrevNombre.Foreground = System.Windows.Media.Brushes.LightGray;
                PrevNombre.FontStyle = FontStyles.Italic;
            }
            else
            {
                PrevNombre.Text = TxtNombre.Text.Trim();
                PrevNombre.Foreground = System.Windows.Media.Brushes.Black;
                PrevNombre.FontStyle = FontStyles.Normal;
            }

            if (string.IsNullOrWhiteSpace(TxtMarca.Text))
            {
                PrevMarca.Text = "Marca no especificada";
                PrevMarca.Foreground = System.Windows.Media.Brushes.LightGray;
            }
            else
            {
                PrevMarca.Text = TxtMarca.Text.Trim();
                PrevMarca.Foreground = System.Windows.Media.Brushes.Gray;
            }

            if (string.IsNullOrWhiteSpace(TxtProveedor.Text))
            {
                PrevProveedor.Text = "—";
                PrevProveedor.Foreground = System.Windows.Media.Brushes.LightGray;
            }
            else
            {
                PrevProveedor.Text = TxtProveedor.Text.Trim();
                PrevProveedor.Foreground = System.Windows.Media.Brushes.Black;
            }

            if (CmbCategoria.SelectedItem is Categoria cat)
            {
                PrevCategoriaBadge.Visibility = Visibility.Visible;
                PrevCategoriaVacia.Visibility = Visibility.Collapsed;
                PrevCategoriaTexto.Text = cat.Nombre;
            }
            else
            {
                PrevCategoriaBadge.Visibility = Visibility.Collapsed;
                PrevCategoriaVacia.Visibility = Visibility.Visible;
            }

            ActualizarResumenVariantes();
        }

        private void ActualizarResumenVariantes()
        {
            if (_variantes.Count == 0)
            {
                PrevContadorBadge.Visibility = Visibility.Collapsed;
                PrevContadorVacio.Visibility = Visibility.Visible;
                PrevRangoPrecios.Visibility = Visibility.Collapsed;
                PrevVariantesVacia.Visibility = Visibility.Visible;
            }
            else
            {
                var count = _variantes.Count;
                PrevContadorTexto.Text = count == 1 ? "1 variante" : $"{count} variantes";
                PrevContadorBadge.Visibility = Visibility.Visible;
                PrevContadorVacio.Visibility = Visibility.Collapsed;
                PrevVariantesVacia.Visibility = Visibility.Collapsed;

                var min = _variantes.Min(v => v.PrecioUnitario);
                var max = _variantes.Max(v => v.PrecioUnitario);

                PrevRangoPrecios.Text = min == max
                    ? $"${min:N2}"
                    : $"${min:N2} — ${max:N2}";
                PrevRangoPrecios.Visibility = Visibility.Visible;
            }
        }

        private void ListVariantes_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (ListVariantes.SelectedItem is not VarianteProducto variante) return;

            TxtVarMedida.Text = variante.Medida ?? string.Empty;
            TxtVarCalidad.Text = variante.Calidad == "—" ? string.Empty : variante.Calidad ?? string.Empty;
            TxtVarPrecio.Text = variante.PrecioUnitario.ToString("0.##", new System.Globalization.CultureInfo("es-AR"));
            TxtVarStock.Text = variante.StockActual.ToString();

            _varianteEnEdicion = variante;

            BtnConfirmarVariante.Content = "✓";
            BtnConfirmarVariante.ToolTip = "Confirmar cambios";
            BtnCancelarEdicionVar.Visibility = Visibility.Visible;
            PanelHintEdicion.Visibility = Visibility.Visible;
            TxtHintDobleClick.Visibility = Visibility.Collapsed;

            TxtVarMedida.Focus();
        }

        private void BtnConfirmarVariante_Click(object sender, RoutedEventArgs e)
        {
            var medida = TxtVarMedida.Text.Trim();
            var calidad = TxtVarCalidad.Text.Trim();
            var precioTxt = TxtVarPrecio.Text.Trim();
            var stockTxt = TxtVarStock.Text.Trim();

            LimpiarErrorVariante();

            if (string.IsNullOrEmpty(medida))
            {
                MostrarErrorVariante(TxtVarMedida, "Ingresá la medida de la variante (ej: 50 kg, 1/2\", 4 m).");
                return;
            }

            if (precioTxt.Length == 0)
            {
                MostrarErrorVariante(TxtVarPrecio, "Ingresá el precio. Ejemplos: 1500 · 1.500 · 1.500,50");
                return;
            }

            if (!TryParsePrecio(precioTxt, out var precio))
            {
                MostrarErrorVariante(TxtVarPrecio, $"\"{precioTxt}\" no es un precio válido. Ejemplos: 1500 · 1.500 · 1.500,50");
                return;
            }

            if (precio <= 0)
            {
                MostrarErrorVariante(TxtVarPrecio, "El precio tiene que ser mayor a $0.");
                return;
            }

            int stock = 0;
            if (stockTxt.Length > 0 && (!int.TryParse(stockTxt, out stock) || stock < 0))
            {
                MostrarErrorVariante(TxtVarStock, "El stock tiene que ser un número entero, 0 o mayor.");
                return;
            }

            if (_varianteEnEdicion != null)
            {
                var idx = _variantes.IndexOf(_varianteEnEdicion);
                if (idx >= 0)
                {
                    _varianteEnEdicion.Medida = medida;
                    _varianteEnEdicion.Calidad = string.IsNullOrEmpty(calidad) ? "—" : calidad;
                    _varianteEnEdicion.PrecioUnitario = precio;
                    _varianteEnEdicion.StockActual = stock;

                    _variantes.RemoveAt(idx);
                    _variantes.Insert(idx, _varianteEnEdicion);
                }

                SalirModoEdicionVariante();
            }
            else
            {
                _variantes.Add(new VarianteProducto
                {
                    Medida = medida,
                    Calidad = string.IsNullOrEmpty(calidad) ? "—" : calidad,
                    PrecioUnitario = precio,
                    StockActual = stock,
                    StockMinimo = 5
                });
            }

            LimpiarCamposVariante();
            ActualizarEstadoTablaVariantes();
            ActualizarResumenVariantes();
        }

        private void BtnCancelarEdicionVar_Click(object sender, RoutedEventArgs e)
        {
            SalirModoEdicionVariante();
            LimpiarCamposVariante();
        }

        private void SalirModoEdicionVariante()
        {
            _varianteEnEdicion = null;
            ListVariantes.SelectedItem = null;

            BtnConfirmarVariante.Content = "+";
            BtnConfirmarVariante.ToolTip = "Agregar variante";
            BtnCancelarEdicionVar.Visibility = Visibility.Collapsed;
            PanelHintEdicion.Visibility = Visibility.Collapsed;

            if (_variantes.Count > 0)
                TxtHintDobleClick.Visibility = Visibility.Visible;
        }

        private void LimpiarCamposVariante()
        {
            TxtVarMedida.Clear();
            TxtVarCalidad.Clear();
            TxtVarPrecio.Clear();
            TxtVarStock.Clear();
            TxtVarMedida.Focus();
        }

        private void BtnEliminarVariante_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is VarianteProducto variante)
            {
                if (_varianteEnEdicion == variante)
                {
                    SalirModoEdicionVariante();
                    LimpiarCamposVariante();
                }

                if (variante.Id > 0 && _repo.VarianteTieneVentas(variante.Id))
                {
                    Dialogo.Show(
                        $"La variante \"{variante.Medida}\" tiene ventas registradas y no se puede eliminar sin perder ese historial.\n\n" +
                        "Si ya no la vendés, podés dejar su stock en 0.",
                        "No se puede eliminar", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var r = Dialogo.Show(
                    $"¿Eliminás la variante \"{variante.Medida}\"?",
                    "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (r == MessageBoxResult.Yes)
                {
                    _variantes.Remove(variante);
                    ActualizarEstadoTablaVariantes();
                    ActualizarResumenVariantes();
                }
            }
        }

        private void ActualizarEstadoTablaVariantes()
        {
            var hayVariantes = _variantes.Count > 0;
            TxtVacioVariantes.Visibility = hayVariantes ? Visibility.Collapsed : Visibility.Visible;
            TxtHintDobleClick.Visibility = hayVariantes ? Visibility.Visible : Visibility.Collapsed;

            if (hayVariantes)
                TxtErrorVariantes.Visibility = Visibility.Collapsed;
        }

        private bool Validar()
        {
            bool ok = true;

            TxtErrorCodigo.Visibility = Visibility.Collapsed;
            TxtErrorNombre.Visibility = Visibility.Collapsed;
            TxtErrorCategoria.Visibility = Visibility.Collapsed;
            TxtErrorVariantes.Visibility = Visibility.Collapsed;

            var numero = TxtCodigoNumero.Text.Trim();

            if (CmbCategoria.SelectedItem is not Categoria)
            {
                TxtErrorCodigo.Text = "Elegí la categoría para armar el código.";
                TxtErrorCodigo.Visibility = Visibility.Visible;
                ok = false;
            }
            else if (numero.Length == 0)
            {
                TxtErrorCodigo.Text = "Ingresá el número del código.";
                TxtErrorCodigo.Visibility = Visibility.Visible;
                ok = false;
            }
            else if (!int.TryParse(numero, out var n) || n <= 0)
            {
                TxtErrorCodigo.Text = "El número del código tiene que ser mayor a 0.";
                TxtErrorCodigo.Visibility = Visibility.Visible;
                ok = false;
            }

            if (string.IsNullOrWhiteSpace(TxtNombre.Text))
            {
                TxtErrorNombre.Text = "El nombre es obligatorio.";
                TxtErrorNombre.Visibility = Visibility.Visible;
                ok = false;
            }

            if (string.IsNullOrWhiteSpace(CmbCategoria.Text))
            {
                TxtErrorCategoria.Text = "La categoría es obligatoria.";
                TxtErrorCategoria.Visibility = Visibility.Visible;
                ok = false;
            }

            if (_variantes.Count == 0)
            {
                TxtErrorVariantes.Visibility = Visibility.Visible;
                ok = false;
            }

            return ok;
        }

        private void BtnGuardar_Click(object sender, RoutedEventArgs e)
        {
            if (_varianteEnEdicion != null)
            {
                var r = Dialogo.Show(
                    "Tenés una variante en edición sin confirmar.\n¿Querés descartarla y guardar igual?",
                    "Edición pendiente", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (r != MessageBoxResult.Yes) return;

                SalirModoEdicionVariante();
                LimpiarCamposVariante();
            }

            if (!Validar()) return;

            var codigo = CodigoActual;
            if (!VerificarCodigoDisponible(codigo)) return;

            var producto = new Producto
            {
                Id = _productoOriginal?.Id ?? 0,
                CodigoInterno = codigo,
                Nombre = TxtNombre.Text.Trim(),
                Marca = TxtMarca.Text.Trim().NullIfEmpty(),
                Proveedor = TxtProveedor.Text.Trim().NullIfEmpty(),
                CategoriaId = (CmbCategoria.SelectedItem as Categoria)?.Id ?? 0,
                Variantes = _variantes.ToList()
            };

            try
            {
                BtnGuardar.IsEnabled = false;
                BtnGuardar.Content = "Guardando…";

                if (_modoEdicion)
                    _repo.ActualizarProducto(producto);
                else
                    _repo.InsertarProducto(producto);

                DialogResult = true;
                Close();
            }
            catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number is 2627 or 2601)
            {
                // Respaldo por si el código se cargó desde otra PC entre la verificación y el guardado
                TxtErrorCodigo.Text = $"Ya existe un material con el código {producto.CodigoInterno}.";
                TxtErrorCodigo.Visibility = Visibility.Visible;
                RestaurarBotonGuardar();
            }
            catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number == 547)
            {
                Dialogo.Show(
                    "No se pudo guardar porque una de las variantes eliminadas tiene ventas registradas.\n" +
                    "Volvé a agregarla o dejá su stock en 0.",
                    "No se puede guardar", MessageBoxButton.OK, MessageBoxImage.Warning);
                RestaurarBotonGuardar();
            }
            catch (Exception ex)
            {
                Dialogo.Show(
                    $"Error al guardar:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                RestaurarBotonGuardar();
            }
        }

        private void RestaurarBotonGuardar()
        {
            BtnGuardar.IsEnabled = true;
            BtnGuardar.Content = _modoEdicion ? "Guardar cambios" : "Guardar material";
        }

        /// <summary>
        /// Devuelve true si se puede guardar con este código. Si pertenece a un producto
        /// desactivado (y estamos dando de alta), ofrece reactivarlo en lugar de duplicarlo.
        /// </summary>
        private bool VerificarCodigoDisponible(string codigo)
        {
            (int id, string nombre, bool activo)? existente;
            try
            {
                existente = _repo.ObtenerPorCodigo(codigo);
            }
            catch
            {
                return true; // si falla la consulta, el guardado lo valida igual
            }

            if (existente == null || existente.Value.id == (_productoOriginal?.Id ?? 0))
                return true;

            var (id, nombre, activo) = existente.Value;

            if (activo || _modoEdicion)
            {
                TxtErrorCodigo.Text = activo
                    ? $"El código {codigo} ya lo usa \"{nombre}\"."
                    : $"El código {codigo} ya lo usa \"{nombre}\" (desactivado).";
                TxtErrorCodigo.Visibility = Visibility.Visible;
                TxtCodigoNumero.Focus();
                return false;
            }

            var res = Dialogo.Show(
                $"El código {codigo} pertenece a \"{nombre}\", que está desactivado.\n\n" +
                "¿Querés reactivarlo? Vuelve con el stock y precios que tenía (los datos cargados en este formulario no se guardan).\n\n" +
                "Elegí \"No\" para usar otro código.",
                "Producto desactivado", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (res == MessageBoxResult.Yes)
            {
                try
                {
                    _repo.ReactivarProducto(id);
                    Dialogo.Show($"\"{nombre}\" fue reactivado. Podés editarlo desde el inventario.",
                        "Producto reactivado", MessageBoxButton.OK, MessageBoxImage.Information);
                    DialogResult = true;
                    Close();
                }
                catch (Exception ex)
                {
                    Dialogo.Show($"Error al reactivar:\n{ex.Message}",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                TxtCodigoNumero.Focus();
                TxtCodigoNumero.SelectAll();
            }

            return false;
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BtnCerrar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        // ── Errores de la fila de variante: texto rojo debajo y borde del campo en rojo ──

        private static readonly System.Windows.Media.Brush _bordeNormal =
            new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)
                System.Windows.Media.ColorConverter.ConvertFromString("#222222"));
        private static readonly System.Windows.Media.Brush _bordeError =
            new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)
                System.Windows.Media.ColorConverter.ConvertFromString("#C62828"));

        private void MostrarErrorVariante(TextBox campo, string mensaje)
        {
            TxtErrorVarianteInput.Text = "⚠  " + mensaje;
            TxtErrorVarianteInput.Visibility = Visibility.Visible;
            campo.BorderBrush = _bordeError;
            campo.Focus();
            campo.SelectAll();
        }

        private void LimpiarErrorVariante()
        {
            if (TxtErrorVarianteInput == null) return;
            TxtErrorVarianteInput.Visibility = Visibility.Collapsed;
            TxtVarMedida.BorderBrush = _bordeNormal;
            TxtVarPrecio.BorderBrush = _bordeNormal;
            TxtVarStock.BorderBrush = _bordeNormal;
        }

        private void CampoVariante_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tb && tb.BorderBrush == _bordeError)
                LimpiarErrorVariante();
        }

        /// <summary>Stock: solo dígitos (no deja escribir signos negativos ni letras).</summary>
        private void SoloDigitos_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            e.Handled = !e.Text.All(char.IsDigit);
        }

        /// <summary>Precio: dígitos y separadores (punto, coma, signo $).</summary>
        private void Precio_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            e.Handled = !e.Text.All(c => char.IsDigit(c) || c is '.' or ',' or '$');
        }
    }

    internal static class StringExtensions
    {
        public static string? NullIfEmpty(this string s) =>
            string.IsNullOrWhiteSpace(s) ? null : s;
    }
}