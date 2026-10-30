using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace ElApostol_WPF.UI.Shared
{
    /// <summary>
    /// Reemplazo de MessageBox con el estilo de la aplicación.
    /// Misma firma que MessageBox.Show para poder usarlo en su lugar.
    /// </summary>
    public partial class Dialogo : Window
    {
        private MessageBoxResult _resultado;
        private readonly MessageBoxButton _botones;

        private Dialogo(string mensaje, string titulo, MessageBoxButton botones, MessageBoxImage icono,
                        string? textoSi, string? textoNo)
        {
            InitializeComponent();
            _botones = botones;

            TxtTitulo.Text = string.IsNullOrWhiteSpace(titulo) ? "El Apóstol" : titulo;
            TxtMensaje.Text = mensaje;

            var (acento, fondo, simbolo) = icono switch
            {
                MessageBoxImage.Error    => ("#C62828", "#FCEBEB", "✕"),
                MessageBoxImage.Warning  => ("#E08A00", "#FFF4E0", "!"),
                MessageBoxImage.Question => ("#1565C0", "#E3EEFA", "?"),
                _                        => ("#2E7D32", "#EAF3DE", "i"),
            };
            var brushAcento = Pincel(acento);
            Franja.Background = brushAcento;
            IconoFondo.Background = Pincel(fondo);
            IconoTexto.Foreground = brushAcento;
            IconoTexto.Text = simbolo;

            if (botones == MessageBoxButton.YesNo)
            {
                BtnSi.Content = textoSi ?? "Sí";
                BtnNo.Content = textoNo ?? "No";
                BtnNo.Visibility = Visibility.Visible;
                _resultado = MessageBoxResult.No;
                // En advertencias el botón de confirmar va en el color del aviso
                if (icono is MessageBoxImage.Warning or MessageBoxImage.Error)
                    BtnSi.Background = brushAcento;
            }
            else
            {
                BtnSi.Content = textoSi ?? "Aceptar";
                _resultado = MessageBoxResult.OK;
            }

            Loaded += (_, _) => BtnSi.Focus();
        }

        // ── API estilo MessageBox ──

        public static MessageBoxResult Show(string mensaje) =>
            Show(mensaje, "El Apóstol", MessageBoxButton.OK, MessageBoxImage.Information);

        public static MessageBoxResult Show(string mensaje, string titulo) =>
            Show(mensaje, titulo, MessageBoxButton.OK, MessageBoxImage.Information);

        public static MessageBoxResult Show(string mensaje, string titulo, MessageBoxButton botones) =>
            Show(mensaje, titulo, botones, MessageBoxImage.Information);

        public static MessageBoxResult Show(string mensaje, string titulo,
                                            MessageBoxButton botones, MessageBoxImage icono) =>
            Mostrar(mensaje, titulo, botones, icono, null, null);

        /// <summary>Pregunta Sí/No con textos de botón personalizados. Devuelve true si confirma.</summary>
        public static bool Confirmar(string mensaje, string titulo, string textoSi, string textoNo,
                                     MessageBoxImage icono = MessageBoxImage.Question) =>
            Mostrar(mensaje, titulo, MessageBoxButton.YesNo, icono, textoSi, textoNo) == MessageBoxResult.Yes;

        private static MessageBoxResult Mostrar(string mensaje, string titulo, MessageBoxButton botones,
                                                MessageBoxImage icono, string? textoSi, string? textoNo)
        {
            var dlg = new Dialogo(mensaje, titulo, botones, icono, textoSi, textoNo);

            var owner = BuscarVentanaActiva();
            if (owner != null) dlg.Owner = owner;
            else dlg.WindowStartupLocation = WindowStartupLocation.CenterScreen;

            dlg.ShowDialog();
            return dlg._resultado;
        }

        private static Window? BuscarVentanaActiva()
        {
            if (Application.Current == null) return null;
            var ventanas = Application.Current.Windows.OfType<Window>()
                .Where(w => w.IsVisible && w is not Dialogo)
                .ToList();
            return ventanas.FirstOrDefault(w => w.IsActive) ?? ventanas.LastOrDefault();
        }

        private static SolidColorBrush Pincel(string hex) =>
            new((Color)ColorConverter.ConvertFromString(hex));

        // ── Eventos ──

        private void BtnSi_Click(object sender, RoutedEventArgs e)
        {
            _resultado = _botones == MessageBoxButton.YesNo ? MessageBoxResult.Yes : MessageBoxResult.OK;
            Close();
        }

        private void BtnNo_Click(object sender, RoutedEventArgs e)
        {
            _resultado = MessageBoxResult.No;
            Close();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                _resultado = _botones == MessageBoxButton.YesNo ? MessageBoxResult.No : MessageBoxResult.OK;
                Close();
            }
        }

        private void Cuerpo_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed) DragMove();
        }
    }
}
