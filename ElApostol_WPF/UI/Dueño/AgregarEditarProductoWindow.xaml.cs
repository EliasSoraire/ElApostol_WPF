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
            TxtVarPrecio.Text = variante.PrecioUnitario.ToString("F2");
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

            if (!decimal.TryParse(precioTxt.Replace(',', '.'),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var precio) || precio <= 0)
            {
                MostrarError("El precio de la variante debe ser un número positivo.");
                TxtVarPrecio.Focus();
                return;
            }

            int.TryParse(stockTxt, out int stock);

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

            if (string.IsNullOrWhiteSpace(TxtCodigo.Text))
            {
                TxtErrorCodigo.Text = "El código es obligatorio.";
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
                var r = MessageBox.Show(
                    "Tenés una variante en edición sin confirmar.\n¿Querés descartarla y guardar igual?",
                    "Edición pendiente", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (r != MessageBoxResult.Yes) return;

                SalirModoEdicionVariante();
                LimpiarCamposVariante();
            }

            if (!Validar()) return;

            var producto = new Producto
            {
                Id = _productoOriginal?.Id ?? 0,
                CodigoInterno = TxtCodigo.Text.Trim(),
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
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error al guardar:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);

                BtnGuardar.IsEnabled = true;
                BtnGuardar.Content = _modoEdicion ? "Guardar cambios" : "Guardar material";
            }
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