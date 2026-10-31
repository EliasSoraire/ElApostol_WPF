using System.Windows;
using ElApostol_WPF.UI.Shared;
using System.Windows.Controls;
using System.Windows.Media;
using ElApostol_WPF.Business;
using ElApostol_WPF.Data;
using ElApostol_WPF.Models;

namespace ElApostol_WPF.UI.Dueño
{
    public partial class CambiarContrasenaEmpleadoWindow : Window
    {
        private readonly Usuario _empleado;

        private bool _mostrandoPass1 = false;
        private bool _mostrandoPass2 = false;
        private bool _cargandoDesdeGen = false;

        /// <summary>Se abre desde Gestionar empleados con el empleado ya elegido.</summary>
        public CambiarContrasenaEmpleadoWindow(Usuario empleado)
        {
            InitializeComponent();
            _empleado = empleado;

            TxtAvatar.Text = ObtenerIniciales(empleado.NombreCompleto);
            TxtNombreEmpleado.Text = empleado.NombreCompleto;
            TxtInfoEmpleado.Text = $"@{empleado.NombreUsuario} · Alta: {empleado.FechaAlta:dd/MM/yyyy}";
            PanelInfoEmpleado.Visibility = Visibility.Visible;
        }
        private void BtnGenerar_Click(object sender, RoutedEventArgs e)
        {
            var pass = GenerarContrasenaAleatoria();

            TxtGenerada.Text = pass;
            TxtGenerada.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A1A1A"));
            BtnCopiar.Visibility = Visibility.Visible;

            _cargandoDesdeGen = true;

            _mostrandoPass1 = true;
            PbPass1.Visibility = Visibility.Collapsed;
            TxtPass1Visible.Visibility = Visibility.Visible;
            TxtPass1Visible.Text = pass;

            _mostrandoPass2 = true;
            PbPass2.Visibility = Visibility.Collapsed;
            TxtPass2Visible.Visibility = Visibility.Visible;
            TxtPass2Visible.Text = pass;

            _cargandoDesdeGen = false;
            VerificarCoincidencia();
        }

        private void BtnCopiar_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(TxtGenerada.Text);
            BtnCopiar.Content = "¡Copiado!";
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1.5)
            };
            timer.Tick += (_, _) => { BtnCopiar.Content = "Copiar"; timer.Stop(); };
            timer.Start();
        }

        private void BtnOjoPass1_Click(object sender, RoutedEventArgs e)
        {
            _mostrandoPass1 = !_mostrandoPass1;
            if (_mostrandoPass1)
            {
                TxtPass1Visible.Text = PbPass1.Password;
                PbPass1.Visibility = Visibility.Collapsed;
                TxtPass1Visible.Visibility = Visibility.Visible;
            }
            else
            {
                PbPass1.Password = TxtPass1Visible.Text;
                TxtPass1Visible.Visibility = Visibility.Collapsed;
                PbPass1.Visibility = Visibility.Visible;
            }
        }

        private void BtnOjoPass2_Click(object sender, RoutedEventArgs e)
        {
            _mostrandoPass2 = !_mostrandoPass2;
            if (_mostrandoPass2)
            {
                TxtPass2Visible.Text = PbPass2.Password;
                PbPass2.Visibility = Visibility.Collapsed;
                TxtPass2Visible.Visibility = Visibility.Visible;
            }
            else
            {
                PbPass2.Password = TxtPass2Visible.Text;
                TxtPass2Visible.Visibility = Visibility.Collapsed;
                PbPass2.Visibility = Visibility.Visible;
            }
        }

        private void Pass_Changed(object sender, RoutedEventArgs e)
        {
            if (_cargandoDesdeGen) return;
            VerificarCoincidencia();
        }

        private void TxtPassVisible_Changed(object sender, TextChangedEventArgs e)
        {
            if (_cargandoDesdeGen) return;
            VerificarCoincidencia();
        }

        private void VerificarCoincidencia()
        {
            var p1 = _mostrandoPass1 ? TxtPass1Visible.Text : PbPass1.Password;
            var p2 = _mostrandoPass2 ? TxtPass2Visible.Text : PbPass2.Password;

            if (string.IsNullOrEmpty(p2))
            {
                TxtMatchHint.Visibility = Visibility.Collapsed;
            }
            else if (p1 == p2 && p1.Length < 6)
            {
                TxtMatchHint.Visibility = Visibility.Visible;
                TxtMatchHint.Text = "✖ Mínimo 6 caracteres";
                TxtMatchHint.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#791F1F"));
            }
            else if (p1 == p2)
            {
                TxtMatchHint.Visibility = Visibility.Visible;
                TxtMatchHint.Text = "✔ Las contraseñas coinciden";
                TxtMatchHint.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27500A"));
            }
            else
            {
                TxtMatchHint.Visibility = Visibility.Visible;
                TxtMatchHint.Text = "✖ Las contraseñas no coinciden";
                TxtMatchHint.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#791F1F"));
            }

            VerificarGuardar();
        }

        private void VerificarGuardar()
        {
            var p1 = _mostrandoPass1 ? TxtPass1Visible.Text : PbPass1.Password;
            var p2 = _mostrandoPass2 ? TxtPass2Visible.Text : PbPass2.Password;

            BtnGuardar.IsEnabled = p1.Length >= 6 && p1 == p2;
        }

        private void BtnGuardar_Click(object sender, RoutedEventArgs e)
        {
            var emp = _empleado;
            var pass = _mostrandoPass1 ? TxtPass1Visible.Text : PbPass1.Password;

            try
            {
                var auth = new AuthService();
                var ok = auth.CambiarContrasena(emp.Id, pass, esTemporal: true);

                if (!ok)
                {
                    Dialogo.Show("No se pudo actualizar la contraseña.", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                Dialogo.Show(
                    $"Contraseña de {emp.NombreCompleto} actualizada correctamente.\nEl empleado deberá cambiarla al ingresar.",
                    "Contraseña guardada",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                Dialogo.Show($"Error al guardar:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private static string ObtenerIniciales(string nombre)
        {
            var partes = nombre.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return partes.Length >= 2
                ? $"{partes[0][0]}{partes[1][0]}".ToUpper()
                : nombre[..Math.Min(2, nombre.Length)].ToUpper();
        }

        private static string GenerarContrasenaAleatoria()
        {
            const string chars = "ABCDEFGHJKMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789!@#";
            var rng = new Random();
            return new string(Enumerable.Range(0, 10).Select(_ => chars[rng.Next(chars.Length)]).ToArray());
        }
    }
}