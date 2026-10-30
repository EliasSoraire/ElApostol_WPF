using System.Collections.ObjectModel;
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
            TxtCodigo.Text = p.CodigoInterno;
            TxtNombre.Text = p.Nombre;
            TxtMarca.Text = p.Marca ?? string.Empty;
            TxtProveedor.Text = p.Proveedor ?? string.Empty;

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

        private string? _codigoSugerido;

        private void CmbCategoria_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // En alta, si el código está vacío o es el que sugerimos antes, proponemos el siguiente de la categoría
            if (!_modoEdicion && CmbCategoria.SelectedItem is Categoria cat)
            {
                var actual = TxtCodigo.Text.Trim();
                if (actual.Length == 0 || actual == _codigoSugerido)
                {
                    try
                    {
                        _codigoSugerido = _repo.GenerarSiguienteCodigo(ObtenerPrefijo(cat));
                        TxtCodigo.Text = _codigoSugerido;
                    }
                    catch { }
                }
            }

            ActualizarPreview();
        }

        /// <summary>Prefijo de código según la categoría: primeras 3 letras sin acentos (Construcción → CON).</summary>
        private static string ObtenerPrefijo(Categoria cat)
        {
            var sinAcentos = new string(cat.Nombre
                .Normalize(System.Text.NormalizationForm.FormD)
                .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
                            != System.Globalization.UnicodeCategory.NonSpacingMark)
                .Where(char.IsLetter)
                .ToArray());

            return sinAcentos[..Math.Min(3, sinAcentos.Length)].ToUpperInvariant();
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
            PrevCodigo.Text = string.IsNullOrWhiteSpace(TxtCodigo.Text)
                ? "— sin código —" : TxtCodigo.Text.Trim();
            PrevCodigo.Foreground = string.IsNullOrWhiteSpace(TxtCodigo.Text)
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

            if (string.IsNullOrEmpty(medida))
            {
                MostrarError("La medida es obligatoria para la variante.");
                TxtVarMedida.Focus();
                return;
            }

            if (!TryParsePrecio(precioTxt, out var precio) || precio <= 0)
            {
                MostrarError("El precio de la variante debe ser un número positivo.\nEjemplos válidos: 1500 · 1.500 · 1.500,50");
                TxtVarPrecio.Focus();
                return;
            }

            int stock = 0;
            if (stockTxt.Length > 0 && (!int.TryParse(stockTxt, out stock) || stock < 0))
            {
                MostrarError("El stock debe ser un número entero mayor o igual a 0.");
                TxtVarStock.Focus();
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
                    MessageBox.Show(
                        $"La variante \"{variante.Medida}\" tiene ventas registradas y no se puede eliminar sin perder ese historial.\n\n" +
                        "Si ya no la vendés, podés dejar su stock en 0.",
                        "No se puede eliminar", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var r = MessageBox.Show(
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

            var codigo = TxtCodigo.Text.Trim().ToUpperInvariant();
            var cambioCodigo = !_modoEdicion
                || !string.Equals(codigo, _productoOriginal?.CodigoInterno, StringComparison.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(codigo))
            {
                TxtErrorCodigo.Text = "El código es obligatorio.";
                TxtErrorCodigo.Visibility = Visibility.Visible;
                ok = false;
            }
            else if (cambioCodigo && CmbCategoria.SelectedItem is Categoria catSel)
            {
                // Formato PRE-000, con el prefijo de la categoría (CON, ELE, PLO…)
                var prefijo = ObtenerPrefijo(catSel);
                if (!System.Text.RegularExpressions.Regex.IsMatch(codigo, $@"^{prefijo}-\d{{3,}}$"))
                {
                    TxtErrorCodigo.Text = $"El código debe tener el formato {prefijo}-000 (ej: {prefijo}-001).";
                    TxtErrorCodigo.Visibility = Visibility.Visible;
                    ok = false;
                }
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
                var r = MessageBox.Show(
                    "Tenés una variante en edición sin confirmar.\n¿Querés descartarla y guardar igual?",
                    "Edición pendiente", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (r != MessageBoxResult.Yes) return;

                SalirModoEdicionVariante();
                LimpiarCamposVariante();
            }

            if (!Validar()) return;

            var codigo = TxtCodigo.Text.Trim().ToUpperInvariant();
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
                MessageBox.Show(
                    "No se pudo guardar porque una de las variantes eliminadas tiene ventas registradas.\n" +
                    "Volvé a agregarla o dejá su stock en 0.",
                    "No se puede guardar", MessageBoxButton.OK, MessageBoxImage.Warning);
                RestaurarBotonGuardar();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
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
                TxtCodigo.Focus();
                return false;
            }

            var res = MessageBox.Show(
                $"El código {codigo} pertenece a \"{nombre}\", que está desactivado.\n\n" +
                "¿Querés reactivarlo? Vuelve con el stock y precios que tenía (los datos cargados en este formulario no se guardan).\n\n" +
                "Elegí \"No\" para usar otro código.",
                "Producto desactivado", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (res == MessageBoxResult.Yes)
            {
                try
                {
                    _repo.ReactivarProducto(id);
                    MessageBox.Show($"\"{nombre}\" fue reactivado. Podés editarlo desde el inventario.",
                        "Producto reactivado", MessageBoxButton.OK, MessageBoxImage.Information);
                    DialogResult = true;
                    Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al reactivar:\n{ex.Message}",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                TxtCodigo.Focus();
                TxtCodigo.SelectAll();
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

        private static void MostrarError(string msg) =>
            MessageBox.Show(msg, "Atención", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    internal static class StringExtensions
    {
        public static string? NullIfEmpty(this string s) =>
            string.IsNullOrWhiteSpace(s) ? null : s;
    }
}