using System.Windows;
using ElApostol_WPF.UI.Shared;
using System.Windows.Controls;
using System.Windows.Media;
using ElApostol_WPF.Business;
using ElApostol_WPF.Models;

namespace ElApostol_WPF.UI.Login
{
    public partial class ConfiguracionInicialWindow : Window
    {
        private readonly Usuario _usuario;
        private readonly AuthService _auth = new();

        private string _nuevaContrasena = string.Empty;

        private bool _verNueva1 = false;
        private bool _verCfm1 = false;

        public ConfiguracionInicialWindow(Usuario usuario)
        {
            InitializeComponent();
            _usuario = usuario;
            CmbPregunta.SelectedIndex = 0;
        }

        private void BtnOjo1_Click(object sender, RoutedEventArgs e)
            => ToggleOjo(ref _verNueva1, PbNueva1, TxtNueva1Visible, BtnOjo1);

        private void BtnOjoCfm1_Click(object sender, RoutedEventArgs e)
            => ToggleOjo(ref _verCfm1, PbConfirmar1, TxtConfirmar1Visible, BtnOjoCfm1);

        private void Pass1_Changed(object sender, RoutedEventArgs e)
            => ValidarPaso1();

        private void Pass1Txt_Changed(object sender, TextChangedEventArgs e)
            => ValidarPaso1();

        private void ValidarPaso1()
        {
            var p1 = _verNueva1 ? TxtNueva1Visible.Text : PbNueva1.Password;
            var p2 = _verCfm1 ? TxtConfirmar1Visible.Text : PbConfirmar1.Password;

            EvaluarSeguridad(p1);

            if (string.IsNullOrEmpty(p2))
            {
                TxtMatchPaso1.Visibility = Visibility.Collapsed;
                BtnSiguiente1.IsEnabled = false;
                return;
            }

            TxtMatchPaso1.Visibility = Visibility.Visible;

            if (p1 == p2 && p1.Length >= 6)
            {
                TxtMatchPaso1.Text = "✔ Las contraseñas coinciden";
                TxtMatchPaso1.Foreground = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#27500A"));
                BtnSiguiente1.IsEnabled = true;
            }
            else if (p1 != p2)
            {
                TxtMatchPaso1.Text = "✖ Las contraseñas no coinciden";
                TxtMatchPaso1.Foreground = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#791F1F"));
                BtnSiguiente1.IsEnabled = false;
            }
            else
            {
                TxtMatchPaso1.Text = "✖ Mínimo 6 caracteres";
                TxtMatchPaso1.Foreground = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#791F1F"));
                BtnSiguiente1.IsEnabled = false;
            }
        }

        private void BtnSiguiente1_Click(object sender, RoutedEventArgs e)
        {
            _nuevaContrasena = _verNueva1 ? TxtNueva1Visible.Text : PbNueva1.Password;
            IrAPaso(2);
        }

        private void CmbPregunta_Changed(object sender, SelectionChangedEventArgs e)
            => ValidarPaso2();

        private void TxtRespuesta_Changed(object sender, TextChangedEventArgs e)
            => ValidarPaso2();

        private void ValidarPaso2()
        {
            var item = CmbPregunta.SelectedItem as ComboBoxItem;
            var pregunta = item?.Content?.ToString() ?? "";
            var respuesta = TxtRespuesta.Text.Trim();

            BtnGuardarPaso2.IsEnabled =
                !string.IsNullOrEmpty(pregunta) &&
                pregunta != "Elegí una pregunta..." &&
                respuesta.Length >= 2;
        }

        private void BtnGuardarPaso2_Click(object sender, RoutedEventArgs e)
        {
            var item = CmbPregunta.SelectedItem as ComboBoxItem;
            var pregunta = item?.Content?.ToString() ?? "";
            var respuesta = TxtRespuesta.Text.Trim();

            try
            {
                var okPass = _auth.CambiarContrasena(_usuario.Id, _nuevaContrasena, esTemporal: false);
                if (!okPass)
                {
                    Dialogo.Show("No se pudo guardar la contraseña.",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var okPregunta = _auth.GuardarPreguntaSecreta(_usuario.Id, pregunta, respuesta);
                if (!okPregunta)
                {
                    Dialogo.Show("No se pudo guardar la pregunta secreta.",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                IrAPaso(3);
                IniciarCountdown();
            }
            catch (Exception ex)
            {
                Dialogo.Show($"Error al guardar:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnVolverPaso2_Click(object sender, RoutedEventArgs e)
            => IrAPaso(1);

        private void IniciarCountdown()
        {
            int segundos = 3;
            TxtCountdown.Text = $"Abriendo el sistema en {segundos} segundos...";

            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            timer.Tick += (_, _) =>
            {
                segundos--;
                if (segundos > 0)
                {
                    TxtCountdown.Text = $"Abriendo el sistema en {segundos} segundo{(segundos != 1 ? "s" : "")}...";
                }
                else
                {
                    timer.Stop();
                    AbrirMenuDueno();
                }
            };
            timer.Start();
        }

        private void AbrirMenuDueno()
        {
            var repo = new Data.UsuarioRepository();
            var usuarioActualizado = repo.ObtenerPorId(_usuario.Id);

            if (usuarioActualizado != null)
            {
                var menu = new Dueño.MenuDuenoWindow(usuarioActualizado);
                menu.Show();
            }

            Close();
        }

        private void IrAPaso(int paso)
        {
            PanelPaso1.Visibility = paso == 1 ? Visibility.Visible : Visibility.Collapsed;
            PanelPaso2.Visibility = paso == 2 ? Visibility.Visible : Visibility.Collapsed;
            PanelPaso3.Visibility = paso == 3 ? Visibility.Visible : Visibility.Collapsed;
            ActualizarStepper(paso);
        }

        private void ActualizarStepper(int pasoActual)
        {
            ActualizarCirculo(CircStep1, TxtCirc1, TxtStep1, 1, pasoActual);
            ActualizarCirculo(CircStep2, TxtCirc2, TxtStep2, 2, pasoActual);
            ActualizarCirculo(CircStep3, TxtCirc3, TxtStep3, 3, pasoActual);

            LineStep1.Background = pasoActual > 1
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E7D32"))
                : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DDDDDD"));
            LineStep2.Background = pasoActual > 2
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
                btn.Foreground = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#2E7D32"));
                btn.Content = "🙈";
            }
            else
            {
                pb.Password = tb.Text;
                tb.Visibility = Visibility.Collapsed;
                pb.Visibility = Visibility.Visible;
                btn.Foreground = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#AAAAAA"));
                btn.Content = "👁";
            }
        }

        private void EvaluarSeguridad(string pass)
        {
            if (string.IsNullOrEmpty(pass))
            {
                TxtSeguridadPass.Text = "";
                return;
            }

            int score = 0;

            if (pass.Length >= 6) score++;
            if (pass.Length >= 10) score++;
            if (pass.Any(char.IsUpper)) score++;
            if (pass.Any(char.IsDigit)) score++;
            if (pass.Any(ch => !char.IsLetterOrDigit(ch))) score++;

            if (score <= 2)
            {
                TxtSeguridadPass.Text = "Contraseña débil";
                TxtSeguridadPass.Foreground = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#D32F2F"));
            }
            else if (score <= 4)
            {
                TxtSeguridadPass.Text = "Contraseña media";
                TxtSeguridadPass.Foreground = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#F57C00"));
            }
            else
            {
                TxtSeguridadPass.Text = "Contraseña segura";
                TxtSeguridadPass.Foreground = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#2E7D32"));
            }
        }
    }
}