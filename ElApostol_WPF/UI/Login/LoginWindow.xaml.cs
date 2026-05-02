using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ElApostol_WPF.Business;
using ElApostol_WPF.Models;
using ElApostol_WPF.UI.Dueño;

namespace ElApostol_WPF.UI.Login
{
    public partial class LoginWindow : Window
    {
        private readonly AuthService _auth = new();
        private string _rolSeleccionado = "dueño";
        private bool _contrasenaVisible = false;

        private static readonly SolidColorBrush _rojo =
            new((Color)ColorConverter.ConvertFromString("#D32F2F"));
        private static readonly SolidColorBrush _grisBorde =
            new((Color)ColorConverter.ConvertFromString("#CCCCCC"));

        public LoginWindow()
        {
            InitializeComponent();
            PbContrasena.KeyDown += (s, e) => { if (e.Key == Key.Enter) IntentarLogin(); };
            TxtContrasenaVisible.KeyDown += (s, e) => { if (e.Key == Key.Enter) IntentarLogin(); };
        }

        private void BtnRolDueno_Click(object sender, MouseButtonEventArgs e)
            => SeleccionarRol("dueño");

        private void BtnRolEmpleado_Click(object sender, MouseButtonEventArgs e)
            => SeleccionarRol("empleado");

        private void SeleccionarRol(string rol)
        {
            _rolSeleccionado = rol;

            var verde = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E7D32"));
            var verdeLight = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EAF3DE"));
            var verdeTxt = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27500A"));
            var grisLight = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F5F5F5"));
            var grisBorder = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DDDDDD"));
            var grisTxt = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#666666"));

            if (rol == "dueño")
            {
                BtnRolDueno.Background = verdeLight;
                BtnRolDueno.BorderBrush = verde;
                TxtRolDueno.Foreground = verdeTxt;

                BtnRolEmpleado.BorderBrush = grisBorder;
            }
            else
            {
                BtnRolEmpleado.Background = verdeLight;
                BtnRolEmpleado.BorderBrush = verde;
                TxtRolEmpleado.Foreground = verdeTxt;
                IconEmpleado.Background = verde;

                BtnRolDueno.BorderBrush = grisBorder;
            }

            LinkOlvide.IsEnabled = rol == "dueño";
            LinkOlvide.Foreground = rol == "dueño"
                ? grisTxt
                : new SolidColorBrush(Colors.Transparent);

            LimpiarErrores();
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
                    return;
                }

                if (resultado.Rol != _rolSeleccionado)
                {
                    MostrarError("El perfil seleccionado no coincide con el usuario.");
                    return;
                }

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
            TxtUsuario.BorderBrush = _grisBorde;
            PbContrasena.BorderBrush = _grisBorde;
            TxtContrasenaVisible.BorderBrush = _grisBorde;
            TxtError.Visibility = Visibility.Collapsed;
            TxtError.Text = string.Empty;
        }

        private void MarcarError(System.Windows.Controls.TextBox campo, string? mensaje)
        {
            campo.BorderBrush = _rojo;
            if (mensaje != null) MostrarError(mensaje);
        }

        private void MarcarErrorPassword(string mensaje)
        {
            PbContrasena.BorderBrush = _rojo;
            TxtContrasenaVisible.BorderBrush = _rojo;
            MostrarError(mensaje);
        }

        private void MostrarError(string mensaje)
        {
            TxtError.Text = mensaje;
            TxtError.Visibility = Visibility.Visible;
        }

        private void LinkOlvide_Click(object sender, RoutedEventArgs e)
        {
            var ventana = new RecuperacionContrasenaWindow();
            ventana.Owner = this;
            ventana.ShowDialog();
        }
    }
}