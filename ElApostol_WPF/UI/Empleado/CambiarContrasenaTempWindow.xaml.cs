using System.Windows;
using System.Windows.Media;
using ElApostol_WPF.Business;
using ElApostol_WPF.Models;

namespace ElApostol_WPF.UI.Empleado
{
    public partial class CambiarContrasenaTempWindow : Window
    {
        private readonly Usuario _usuario;
        private readonly AuthService _auth = new();

        private bool _verActual = false;
        private bool _verNueva = false;
        private bool _verConfirmar = false;

        public CambiarContrasenaTempWindow(Usuario usuario)
        {
            InitializeComponent();
            _usuario = usuario;
            CargarInfoEmpleado();
        }

        private void CargarInfoEmpleado()
        {
            TxtAvatar.Text = ObtenerIniciales(_usuario.NombreCompleto);
            TxtNombreEmpleado.Text = _usuario.NombreCompleto;
            TxtMetaEmpleado.Text = "Empleado · Primer ingreso";
            ActualizarFuerza();
        }

        private void BtnOjoActual_Click(object sender, RoutedEventArgs e)
            => ToggleOjo(ref _verActual, PbActual, TxtActualVisible, BtnOjoActual);

        private void BtnOjoNueva_Click(object sender, RoutedEventArgs e)
        {
            ToggleOjo(ref _verNueva, PbNueva, TxtNuevaVisible, BtnOjoNueva);
            ActualizarFuerza();
        }

        private void BtnOjoConfirmar_Click(object sender, RoutedEventArgs e)
            => ToggleOjo(ref _verConfirmar, PbConfirmar, TxtConfirmarVisible, BtnOjoConfirmar);

        private static void ToggleOjo(ref bool estado,
            System.Windows.Controls.PasswordBox pb,
            System.Windows.Controls.TextBox tb,
            System.Windows.Controls.Button btn)
        {
            estado = !estado;
            if (estado)
            {
                tb.Text = pb.Password;
                pb.Visibility = Visibility.Collapsed;
                tb.Visibility = Visibility.Visible;
                btn.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E7D32"));
            }
            else
            {
                pb.Password = tb.Text;
                tb.Visibility = Visibility.Collapsed;
                pb.Visibility = Visibility.Visible;
                btn.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#AAAAAA"));
            }
        }

        private void Pass_Changed(object sender, RoutedEventArgs e)
        {
            ActualizarFuerza();
            VerificarCoincidencia();
            VerificarGuardar();
        }

        private void TxtVisible_Changed(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            ActualizarFuerza();
            VerificarCoincidencia();
            VerificarGuardar();
        }

        private void ActualizarFuerza()
        {
            var pass = _verNueva ? TxtNuevaVisible.Text : PbNueva.Password;

            var gris = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DDDDDD"));
            BarFuerza1.Background = gris;
            BarFuerza2.Background = gris;
            BarFuerza3.Background = gris;

            if (string.IsNullOrEmpty(pass))
            {
                TxtFuerza.Text = "Ingresá una contraseña";
                TxtFuerza.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#AAAAAA"));
                return;
            }

            bool tieneNum = pass.Any(char.IsDigit);
            bool largoOk = pass.Length >= 8;
            bool corto = pass.Length >= 6;

            var rojo = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E24B4A"));
            var naranja = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF9F27"));
            var verde = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E7D32"));

            if (!corto)
            {
                BarFuerza1.Background = rojo;
                TxtFuerza.Text = "Débil — muy corta";
                TxtFuerza.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A32D2D"));
            }
            else if (largoOk && tieneNum)
            {
                BarFuerza1.Background = verde;
                BarFuerza2.Background = verde;
                BarFuerza3.Background = verde;
                TxtFuerza.Text = "Fuerte — buena contraseña";
                TxtFuerza.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27500A"));
            }
            else
            {
                BarFuerza1.Background = naranja;
                BarFuerza2.Background = naranja;
                TxtFuerza.Text = "Media — agregá números o más caracteres";
                TxtFuerza.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#633806"));
            }
        }

        private int NivelFuerza()
        {
            var pass = _verNueva ? TxtNuevaVisible.Text : PbNueva.Password;
            if (string.IsNullOrEmpty(pass) || pass.Length < 6) return 1;
            if (pass.Length >= 8 && pass.Any(char.IsDigit)) return 3;
            return 2;
        }

        private void VerificarCoincidencia()
        {
            var nueva = _verNueva ? TxtNuevaVisible.Text : PbNueva.Password;
            var confirm = _verConfirmar ? TxtConfirmarVisible.Text : PbConfirmar.Password;

            if (string.IsNullOrEmpty(confirm))
            {
                TxtMatchHint.Visibility = Visibility.Collapsed;
                return;
            }

            TxtMatchHint.Visibility = Visibility.Visible;
            if (nueva == confirm)
            {
                TxtMatchHint.Text = "✔ Las contraseñas coinciden";
                TxtMatchHint.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27500A"));
            }
            else
            {
                TxtMatchHint.Text = "✖ Las contraseñas no coinciden";
                TxtMatchHint.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#791F1F"));
            }
        }

        private void VerificarGuardar()
        {
            var actual = _verActual ? TxtActualVisible.Text : PbActual.Password;
            var nueva = _verNueva ? TxtNuevaVisible.Text : PbNueva.Password;
            var confirm = _verConfirmar ? TxtConfirmarVisible.Text : PbConfirmar.Password;

            BtnGuardar.IsEnabled =
                !string.IsNullOrEmpty(actual) &&
                !string.IsNullOrEmpty(nueva) &&
                nueva == confirm &&
                NivelFuerza() >= 2;
        }

        private void BtnGuardar_Click(object sender, RoutedEventArgs e)
        {
            var actual = _verActual ? TxtActualVisible.Text : PbActual.Password;
            var nueva = _verNueva ? TxtNuevaVisible.Text : PbNueva.Password;

            if (!_auth.VerificarContrasenaActual(_usuario.Id, actual))
            {
                MessageBox.Show("La contraseña temporal ingresada es incorrecta.",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var ok = _auth.CambiarContrasena(_usuario.Id, nueva, esTemporal: false);
                if (!ok)
                {
                    MessageBox.Show("No se pudo actualizar la contraseña.",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                MessageBox.Show("¡Contraseña actualizada correctamente! Ya podés ingresar al sistema.",
                    "Contraseña guardada", MessageBoxButton.OK, MessageBoxImage.Information);

                _usuario.ContrasenaEsTemporal = false;
                _usuario.ContrasenaHash = string.Empty;

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static string ObtenerIniciales(string nombre)
        {
            var partes = nombre.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return partes.Length >= 2
                ? $"{partes[0][0]}{partes[1][0]}".ToUpper()
                : nombre[..Math.Min(2, nombre.Length)].ToUpper();
        }
    }
}