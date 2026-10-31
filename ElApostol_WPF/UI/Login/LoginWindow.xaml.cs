using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ElApostol_WPF.Business;
using ElApostol_WPF.Models;
using ElApostol_WPF.UI.Dueño;
using ElApostol_WPF.UI.Shared;

namespace ElApostol_WPF.UI.Login
{
    public partial class LoginWindow : Window
    {
        private readonly AuthService _auth = new();
        private bool _contrasenaVisible = false;

        // Usuario (dueño) para el que se ofrece la recuperación tras fallar la contraseña
        private string? _usuarioRecuperable;

        public LoginWindow()
        {
            InitializeComponent();
            TxtUsuario.KeyDown += (s, e) => { if (e.Key == Key.Enter) IntentarLogin(); };
            PbContrasena.KeyDown += (s, e) => { if (e.Key == Key.Enter) IntentarLogin(); };
            TxtContrasenaVisible.KeyDown += (s, e) => { if (e.Key == Key.Enter) IntentarLogin(); };
            PbContrasena.PasswordChanged += (s, e) => LimpiarErrorCampo(PbContrasena);
            TxtContrasenaVisible.TextChanged += (s, e) => LimpiarErrorCampo(TxtContrasenaVisible);
            Loaded += (s, e) => TxtUsuario.Focus();
        }

        private void TxtUsuario_TextChanged(object sender, TextChangedEventArgs e)
        {
            LimpiarErrorCampo(TxtUsuario);

            // Si cambia el usuario, la oferta de recuperación ya no aplica
            if (_usuarioRecuperable != null &&
                !string.Equals(TxtUsuario.Text.Trim(), _usuarioRecuperable, StringComparison.OrdinalIgnoreCase))
            {
                _usuarioRecuperable = null;
                PanelRecuperar.Visibility = Visibility.Collapsed;
            }
        }

        private void BtnOjo_Click(object sender, RoutedEventArgs e)
        {
            _contrasenaVisible = !_contrasenaVisible;

            if (_contrasenaVisible)
            {
                TxtContrasenaVisible.Text = PbContrasena.Password;
                TxtContrasenaVisible.Visibility = Visibility.Visible;
                PbContrasena.Visibility = Visibility.Collapsed;
                BtnOjo.Foreground = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#2E7D32"));
            }
            else
            {
                PbContrasena.Password = TxtContrasenaVisible.Text;
                PbContrasena.Visibility = Visibility.Visible;
                TxtContrasenaVisible.Visibility = Visibility.Collapsed;
                BtnOjo.Foreground = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#AAAAAA"));
            }
        }

        private void BtnIngresar_Click(object sender, RoutedEventArgs e)
            => IntentarLogin();

        private void IntentarLogin()
        {
            LimpiarErrores();

            var nombreUsuario = TxtUsuario.Text.Trim();
            var contrasena = _contrasenaVisible
                ? TxtContrasenaVisible.Text
                : PbContrasena.Password;

            if (string.IsNullOrEmpty(nombreUsuario))
            {
                MarcarError(TxtUsuario, "Ingresá tu usuario.");
                TxtUsuario.Focus();
                return;
            }
            if (string.IsNullOrEmpty(contrasena))
            {
                MarcarErrorPassword("Ingresá tu contraseña.");
                return;
            }

            try
            {
                Usuario? resultado = _auth.Login(nombreUsuario, contrasena);

                if (resultado == null)
                {
                    MarcarError(TxtUsuario, null);
                    MarcarErrorPassword("Usuario o contraseña incorrectos.");

                    // Al dueño se le ofrece recuperar la contraseña con su pregunta de seguridad
                    if (_auth.PuedeRecuperarContrasena(nombreUsuario))
                    {
                        _usuarioRecuperable = nombreUsuario;
                        PanelRecuperar.Visibility = Visibility.Visible;
                    }
                    return;
                }

                // El rol lo determina el sistema según el usuario
                AbrirVentanaPorRol(resultado);
            }
            catch (Exception ex)
            {
                MostrarError($"Error de conexión: {ex.Message}");
            }
        }

        private void AbrirVentanaPorRol(Usuario usuario)
        {
            try
            {
                if (usuario.ContrasenaEsTemporal)
                {
                    if (usuario.Rol == "dueño")
                    {
                        var ventana = new ConfiguracionInicialWindow(usuario);
                        ventana.Show();
                        this.Close();
                    }
                    else
                    {
                        var ventana = new Empleado.CambiarContrasenaTempWindow(usuario);
                        bool? dialogResult = ventana.ShowDialog();

                        if (dialogResult == true)
                        {
                            var repo = new Data.UsuarioRepository();
                            var usuarioActualizado = repo.ObtenerPorId(usuario.Id);

                            if (usuarioActualizado != null)
                            {
                                var menu = new Empleado.MenuEmpleadoWindow(usuarioActualizado);
                                menu.Show();
                                this.Close();
                            }
                            else
                            {
                                MostrarError("No se pudo recargar el usuario. Intentá de nuevo.");
                            }
                        }
                    }
                }
                else
                {
                    if (usuario.Rol == "dueño")
                    {
                        var menu = new MenuDuenoWindow(usuario);
                        menu.Show();
                    }
                    else
                    {
                        var menu = new Empleado.MenuEmpleadoWindow(usuario);
                        menu.Show();
                    }
                    this.Close();
                }
            }
            catch (Exception ex)
            {
                MostrarError($"Error al abrir el sistema: {ex.Message}");
            }
        }

        private void LimpiarErrores()
        {
            TxtUsuario.Tag = null;
            PbContrasena.Tag = null;
            TxtContrasenaVisible.Tag = null;
            PanelError.Visibility = Visibility.Collapsed;
            TxtError.Text = string.Empty;
        }

        private void LimpiarErrorCampo(Control campo)
        {
            if (campo.Tag == null) return;
            campo.Tag = null;
            PanelError.Visibility = Visibility.Collapsed;
        }

        private void MarcarError(Control campo, string? mensaje)
        {
            campo.Tag = "error";
            if (mensaje != null) MostrarError(mensaje);
        }

        private void MarcarErrorPassword(string mensaje)
        {
            PbContrasena.Tag = "error";
            TxtContrasenaVisible.Tag = "error";
            MostrarError(mensaje);

            if (_contrasenaVisible) TxtContrasenaVisible.SelectAll();
            else PbContrasena.SelectAll();
            (_contrasenaVisible ? (Control)TxtContrasenaVisible : PbContrasena).Focus();
        }

        private void MostrarError(string mensaje)
        {
            TxtError.Text = mensaje;
            PanelError.Visibility = Visibility.Visible;
        }

        private void BtnRecuperar_Click(object sender, RoutedEventArgs e)
        {
            if (_usuarioRecuperable == null) return;

            var ventana = new RecuperacionContrasenaWindow(_usuarioRecuperable) { Owner = this };
            if (ventana.ShowDialog() != true) return;

            // Contraseña cambiada: limpiar y dejar listo para ingresar con la nueva
            _usuarioRecuperable = null;
            PanelRecuperar.Visibility = Visibility.Collapsed;
            PbContrasena.Password = string.Empty;
            TxtContrasenaVisible.Text = string.Empty;
            LimpiarErrores();

            Dialogo.Show("Tu contraseña se actualizó correctamente.\nIngresá con la nueva contraseña.",
                "Contraseña actualizada", MessageBoxButton.OK, MessageBoxImage.Information);

            (_contrasenaVisible ? (Control)TxtContrasenaVisible : PbContrasena).Focus();
        }
    }
}
