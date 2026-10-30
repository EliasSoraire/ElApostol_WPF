using ElApostol_WPF.Data;
using ElApostol_WPF.Models;
using ElApostol_WPF.UI.Shared;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ElApostol_WPF.UI.Dueño
{
    public partial class GestionCategoriasWindow : Window
    {
        private readonly ProductoRepository _repo = new();
        private List<Categoria> _categorias = new();
        private Categoria? _enEdicion;

        private static readonly Brush _verde = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27500A"));
        private static readonly Brush _rojo = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A32D2D"));
        private static readonly Brush _gris = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#757575"));

        /// <summary>True si se agregó, renombró o eliminó alguna categoría.</summary>
        public bool HuboCambios { get; private set; }

        public GestionCategoriasWindow()
        {
            InitializeComponent();
            Cargar();
            Loaded += (_, _) => TxtNombre.Focus();
        }

        private void Cargar()
        {
            try
            {
                _categorias = _repo.ObtenerCategorias();
                ListaCategorias.ItemsSource = _categorias
                    .Select(c => new CategoriaRow(c, c.Id == _enEdicion?.Id))
                    .ToList();
            }
            catch (Exception ex)
            {
                Dialogo.Show($"No se pudieron cargar las categorías:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>Devuelve null si el nombre es válido, o el motivo por el que no lo es.</summary>
        private string? ValidarNombre(string nombre)
        {
            if (nombre.Length == 0)
                return "";

            var prefijo = Categoria.CalcularPrefijo(nombre);
            if (prefijo.Length < 3)
                return "El nombre necesita al menos 3 letras para armar el prefijo.";

            var otras = _categorias.Where(c => c.Id != _enEdicion?.Id).ToList();

            var mismoNombre = otras.FirstOrDefault(c =>
                string.Equals(c.Nombre.Trim(), nombre, StringComparison.CurrentCultureIgnoreCase));
            if (mismoNombre != null)
                return $"Ya existe la categoría \"{mismoNombre.Nombre}\".";

            var mismoPrefijo = otras.FirstOrDefault(c => c.Prefijo == prefijo);
            if (mismoPrefijo != null)
                return $"El prefijo {prefijo} ya lo usa \"{mismoPrefijo.Nombre}\". Elegí un nombre que empiece distinto.";

            return null;
        }

        private void TxtNombre_TextChanged(object sender, TextChangedEventArgs e)
        {
            var nombre = TxtNombre.Text.Trim();
            var error = ValidarNombre(nombre);

            if (nombre.Length == 0)
            {
                TxtAyuda.Text = "Escribí el nombre para ver el prefijo que van a usar sus códigos.";
                TxtAyuda.Foreground = _gris;
            }
            else if (error != null)
            {
                TxtAyuda.Text = error;
                TxtAyuda.Foreground = _rojo;
            }
            else
            {
                var prefijo = Categoria.CalcularPrefijo(nombre);
                TxtAyuda.Text = $"✓ Prefijo de códigos: {prefijo}  (ej: {prefijo}-001)";
                TxtAyuda.Foreground = _verde;
            }

            BtnGuardar.IsEnabled = nombre.Length > 0 && error == null;
        }

        private void TxtNombre_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && BtnGuardar.IsEnabled)
                BtnGuardar_Click(sender, e);
        }

        private void BtnGuardar_Click(object sender, RoutedEventArgs e)
        {
            var nombre = TxtNombre.Text.Trim();
            if (ValidarNombre(nombre) != null) return;

            try
            {
                if (_enEdicion == null)
                {
                    _repo.InsertarCategoria(nombre);
                }
                else if (!_repo.RenombrarCategoria(_enEdicion.Id, nombre))
                {
                    Dialogo.Show($"No se pudo renombrar \"{_enEdicion.Nombre}\" porque ya tiene productos cargados.",
                        "No se puede editar", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                HuboCambios = true;
                SalirDeEdicion();
                Cargar();
            }
            catch (Exception ex)
            {
                Dialogo.Show($"Error al guardar la categoría:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnEditar_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is not CategoriaRow row) return;

            _enEdicion = row.Categoria;
            TxtTituloForm.Text = $"EDITAR \"{row.Nombre.ToUpper()}\"";
            BtnGuardar.Content = "Guardar";
            BtnCancelarEdicion.Visibility = Visibility.Visible;
            TxtNombre.Text = row.Nombre;
            TxtNombre.Focus();
            TxtNombre.SelectAll();
            Cargar();
        }

        private void BtnCancelarEdicion_Click(object sender, RoutedEventArgs e)
        {
            SalirDeEdicion();
            Cargar();
        }

        private void SalirDeEdicion()
        {
            _enEdicion = null;
            TxtTituloForm.Text = "NUEVA CATEGORÍA";
            BtnGuardar.Content = "+ Agregar";
            BtnCancelarEdicion.Visibility = Visibility.Collapsed;
            TxtNombre.Clear();
            TxtNombre.Focus();
        }

        private void BtnEliminar_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is not CategoriaRow row) return;

            if (!Dialogo.Confirmar($"¿Eliminar la categoría \"{row.Nombre}\"?",
                    "Eliminar categoría", "Eliminar", "Cancelar", MessageBoxImage.Warning))
                return;

            try
            {
                if (!_repo.EliminarCategoria(row.Categoria.Id))
                {
                    Dialogo.Show($"\"{row.Nombre}\" tiene productos cargados (activos o desactivados), así que no se puede eliminar.",
                        "No se puede eliminar", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else
                {
                    HuboCambios = true;
                    if (_enEdicion?.Id == row.Categoria.Id) SalirDeEdicion();
                }
                Cargar();
            }
            catch (Exception ex)
            {
                Dialogo.Show($"Error al eliminar la categoría:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCerrar_Click(object sender, RoutedEventArgs e) => Close();
    }

    public class CategoriaRow
    {
        public Categoria Categoria { get; }
        public string Nombre => Categoria.Nombre;
        public string Prefijo => Categoria.Prefijo;
        public string CantidadProductos => Categoria.CantidadProductos.ToString();
        public bool SePuedeModificar => Categoria.CantidadProductos == 0;
        public string? MotivoBloqueo => SePuedeModificar
            ? null
            : $"Tiene {Categoria.CantidadProductos} producto(s): no se puede renombrar ni eliminar porque sus códigos usan el prefijo {Prefijo}.";
        public Brush FondoFila { get; }

        public CategoriaRow(Categoria c, bool enEdicion)
        {
            Categoria = c;
            FondoFila = enEdicion
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF8E1"))
                : Brushes.Transparent;
        }
    }
}
