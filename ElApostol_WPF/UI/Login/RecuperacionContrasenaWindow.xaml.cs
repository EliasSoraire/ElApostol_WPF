using ElApostol_WPF.Business;
using ElApostol_WPF.Data;
using ElApostol_WPF.Models;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ElApostol_WPF.UI.Login
{
    public partial class RecuperacionContrasenaWindow : Window
    {
        private readonly AuthService _auth = new();
        private readonly UsuarioRepository _repo = new();

        private Usuario? _usuarioDueno;

        private bool _verNueva = false;
        private bool _verCfm = false;

        public RecuperacionContrasenaWindow()
        {
            InitializeComponent();
        }

        private void RPaso1_Changed(object sender, TextChangedEventArgs e)
        {
            var usuario = RTxtUsuario.Text.Trim();
            var respuesta = RTxtRespuesta.Text.Trim();

            if (!string.IsNullOrEmpty(usuario))
            {
                var encontrado = _repo.ObtenerPorNombreUsuario(usuario);
                if (encontrado != null && encontrado.Rol == "dueño" &&
                    !string.IsNullOrEmpty(encontrado.PreguntaSecreta))
                {
                    _usuarioDueno = encontrado;
                    RTxtPreguntaMostrada.Text = encontrado.PreguntaSecreta;
                    RPanelPregunta.Visibility = Visibility.Visible;
                }
                else
                {
                    _usuarioDueno = null;
                    RPanelPregunta.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                _usuarioDueno = null;
                RPanelPregunta.Visibility = Visibility.Collapsed;
            }

            RTxtErrorPaso1.Visibility = Visibility.Collapsed;
            RBtnVerificar.IsEnabled = _usuarioDueno != null && respuesta.Length >= 2;
        }

        private void RBtnVerificar_Click(object sender, RoutedEventArgs e)
        {
            if (_usuarioDueno == null) return;

            var respuesta = RTxtRespuesta.Text.Trim();

            if (!_auth.VerificarRespuestaSecreta(_usuarioDueno.Id, respuesta))
            {
                RTxtErrorPaso1.Text = "Respuesta incorrecta. Intentá de nuevo.";
                RTxtErrorPaso1.Visibility = Visibility.Visible;
                return;
            }

            RTxtErrorPaso1.Visibility = Visibility.Collapsed;
            IrAPaso(2);
        }

        private void RBtnVolverLogin_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void RBtnOjoNueva_Click(object sender, RoutedEventArgs e)
            => ToggleOjo(ref _verNueva, RPbNueva, RTxtNuevaVisible, RBtnOjoNueva);

        private void RBtnOjoCfm_Click(object sender, RoutedEventArgs e)
            => ToggleOjo(ref _verCfm, RPbConfirmar, RTxtConfirmarVisible, RBtnOjoCfm);

        private void RPaso2_Changed(object sender, RoutedEventArgs e)
            => ValidarPaso2();

        private void RPaso2Txt_Changed(object sender, TextChangedEventArgs e)
            => ValidarPaso2();

        private void ValidarPaso2()
        {
            var p1 = _verNueva ? RTxtNuevaVisible.Text : RPbNueva.Password;
            var p2 = _verCfm ? RTxtConfirmarVisible.Text : RPbConfirmar.Password;

            if (string.IsNullOrEmpty(p2))
            {
                RTxtMatchPaso2.Visibility = Visibility.Collapsed;
                RBtnGuardarPass.IsEnabled = false;
                return;
            }

            RTxtMatchPaso2.Visibility = Visibility.Visible;

            if (p1 == p2 && p1.Length >= 6)
            {
                RTxtMatchPaso2.Text = "✔ Las contraseñas coinciden";
                RTxtMatchPaso2.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27500A"));
                RBtnGuardarPass.IsEnabled = true;
            }
            else if (p1 != p2)
            {
                RTxtMatchPaso2.Text = "✖ Las contraseñas no coinciden";
                RTxtMatchPaso2.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#791F1F"));
                RBtnGuardarPass.IsEnabled = false;
            }
            else
            {
                RTxtMatchPaso2.Text = "✖ Mínimo 6 caracteres";
                RTxtMatchPaso2.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#791F1F"));
                RBtnGuardarPass.IsEnabled = false;
            }
        }

        private void RBtnGuardarPass_Click(object sender, RoutedEventArgs e)
        {
            if (_usuarioDueno == null) return;

            var nueva = _verNueva ? RTxtNuevaVisible.Text : RPbNueva.Password;

            try
            {
                var ok = _auth.CambiarContrasena(_usuarioDueno.Id, nueva, esTemporal: false);
                if (!ok)
                {
                    MessageBox.Show("No se pudo guardar la contraseña.",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                IrAPaso(3);
                IniciarCountdown();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RBtnVolverPaso2_Click(object sender, RoutedEventArgs e)
            => IrAPaso(1);

        private void IniciarCountdown()
        {
            int segundos = 3;
            RTxtCountdown.Text = $"Redirigiendo en {segundos} segundos...";

            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            timer.Tick += (_, _) =>
            {
                segundos--;
                if (segundos > 0)
                {
                    RTxtCountdown.Text = $"Redirigiendo en {segundos} segundo{(segundos != 1 ? "s" : "")}...";
                }
                else
                {
                    timer.Stop();
                    DialogResult = true;
                    Close(); 
                }
            };
            timer.Start();
        }

        private void IrAPaso(int paso)
        {
            RPanelPaso1.Visibility = paso == 1 ? Visibility.Visible : Visibility.Collapsed;
            RPanelPaso2.Visibility = paso == 2 ? Visibility.Visible : Visibility.Collapsed;
            RPanelPaso3.Visibility = paso == 3 ? Visibility.Visible : Visibility.Collapsed;
            ActualizarStepper(paso);
        }

        private void ActualizarStepper(int pasoActual)
        {
            ActualizarCirculo(RCirc1, RTxtC1, RTxtS1, 1, pasoActual);
            ActualizarCirculo(RCirc2, RTxtC2, RTxtS2, 2, pasoActual);
            ActualizarCirculo(RCirc3, RTxtC3, RTxtS3, 3, pasoActual);

            RLine1.Background = pasoActual > 1
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E7D32"))
                : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DDDDDD"));
            RLine2.Background = pasoActual > 2
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E7D32"))
                : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DDDDDD"));
        }

        private static void ActualizarCirculo(Border circulo, TextBlock txtNum,
            TextBlock txtLabel, int numPaso, int pasoActual)
        {
            if (numPaso < pasoActual)
            {
                circulo.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E7D32"));
                circulo.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E7D32"));
                circulo.BorderThickness = new Thickness(0);
                txtNum.Text = "✔";
                txtNum.Foreground = new SolidColorBrush(Colors.White);
                txtLabel.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E7D32"));
            }
            else if (numPaso == pasoActual)
            {
                circulo.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EAF3DE"));
                circulo.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E7D32"));
                circulo.BorderThickness = new Thickness(1.5);
                txtNum.Text = numPaso.ToString();
                txtNum.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27500A"));
                txtLabel.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27500A"));
            }
            else
            {
                circulo.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F5F5F5"));
                circulo.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DDDDDD"));
                circulo.BorderThickness = new Thickness(0.5);
                txtNum.Text = numPaso.ToString();
                txtNum.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#AAAAAA"));
                txtLabel.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#AAAAAA"));
            }
        }

        private static void ToggleOjo(ref bool estado,
            PasswordBox pb, TextBox tb, Button btn)
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
    }
}