using ElApostol_WPF.Business;
using ElApostol_WPF.Models;
using ElApostol_WPF.UI.Shared;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ElApostol_WPF.UI.Dueño
{
    /// <summary>
    /// El dueño cambia su propia contraseña y, opcionalmente, su pregunta de seguridad.
    /// Siempre pide la contraseña actual.
    /// </summary>
    public partial class CambiarMiContrasenaWindow : Window
    {
        private readonly AuthService _auth = new();
        private readonly Usuario _usuario;

        public CambiarMiContrasenaWindow(Usuario usuario)
        {
            InitializeComponent();
            _usuario = usuario;

            TxtSubtitulo.Text = $"{usuario.NombreCompleto} · @{usuario.NombreUsuario}";
            TxtPreguntaActual.Text = string.IsNullOrEmpty(usuario.PreguntaSecreta)
                ? "Todavía no tenés una pregunta de seguridad configurada."
                : $"Actual: {usuario.PreguntaSecreta}";
            CmbPregunta.SelectedIndex = 0;

            Loaded += (_, _) => PbActual.Focus();
        }

        private void Campo_Changed(object sender, RoutedEventArgs e) => OcultarError();
        private void TxtRespuesta_Changed(object sender, TextChangedEventArgs e) => OcultarError();
        private void CmbPregunta_Changed(object sender, SelectionChangedEventArgs e) => OcultarError();

        private void ChkPregunta_Changed(object sender, RoutedEventArgs e)
        {
            PanelPregunta.Visibility = ChkPregunta.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            OcultarError();
        }

        private void BtnGuardar_Click(object sender, RoutedEventArgs e)
        {
            var actual = PbActual.Password;
            var nueva = PbNueva.Password;
            var confirmar = PbConfirmar.Password;
            var cambiaPregunta = ChkPregunta.IsChecked == true;
            var cambiaPass = nueva.Length > 0 || confirmar.Length > 0;

            if (string.IsNullOrEmpty(actual))
            {
                MostrarError("Ingresá tu contraseña actual.", PbActual);
                return;
            }
            if (!cambiaPass && !cambiaPregunta)
            {
                MostrarError("Escribí una nueva contraseña o marcá el cambio de pregunta de seguridad.", PbNueva);
                return;
            }
            if (cambiaPass)
            {
                if (nueva.Length < 6)
                {
                    MostrarError("La nueva contraseña debe tener al menos 6 caracteres.", PbNueva);
                    return;
                }
                if (nueva != confirmar)
                {
                    MostrarError("Las contraseñas nuevas no coinciden.", PbConfirmar);
                    return;
                }
                if (nueva == actual)
                {
                    MostrarError("La nueva contraseña tiene que ser distinta de la actual.", PbNueva);
                    return;
                }
            }

            string pregunta = "", respuesta = "";
            if (cambiaPregunta)
            {
                pregunta = CmbPregunta.SelectedIndex > 0
                    ? (CmbPregunta.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? ""
                    : "";
                respuesta = TxtRespuesta.Text.Trim();

                if (pregunta == "")
                {
                    MostrarError("Elegí la nueva pregunta de seguridad.", CmbPregunta);
                    return;
                }
                if (respuesta.Length < 2)
                {
                    MostrarError("La respuesta debe tener al menos 2 caracteres.", TxtRespuesta);
                    return;
                }
            }

            try
            {
                if (!_auth.VerificarContrasenaActual(_usuario.Id, actual))
                {
                    PbActual.Clear();
                    MostrarError("La contraseña actual no es correcta.", PbActual);
                    return;
                }

                if (cambiaPass && !_auth.CambiarContrasena(_usuario.Id, nueva, esTemporal: false))
                {
                    MostrarError("No se pudo guardar la contraseña. Intentá de nuevo.", null);
                    return;
                }

                if (cambiaPregunta)
                {
                    if (!_auth.GuardarPreguntaSecreta(_usuario.Id, pregunta, respuesta))
                    {
                        MostrarError("No se pudo guardar la pregunta de seguridad. Intentá de nuevo.", null);
                        return;
                    }
                    _usuario.PreguntaSecreta = pregunta;
                }

                var hecho = (cambiaPass, cambiaPregunta) switch
                {
                    (true, true) => "Se actualizaron tu contraseña y tu pregunta de seguridad.",
                    (true, false) => "Tu contraseña se actualizó correctamente.",
                    _ => "Tu pregunta de seguridad se actualizó correctamente."
                };
                Dialogo.Show(hecho, "Cambios guardados", MessageBoxButton.OK, MessageBoxImage.Information);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MostrarError($"Error de conexión: {ex.Message}", null);
            }
        }

        private void MostrarError(string mensaje, Control? foco)
        {
            TxtError.Text = mensaje;
            PanelError.Visibility = Visibility.Visible;
            foco?.Focus();
        }

        private void OcultarError() => PanelError.Visibility = Visibility.Collapsed;

        private void BtnCancelar_Click(object sender, RoutedEventArgs e) => Close();

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) Close();
        }
    }
}
