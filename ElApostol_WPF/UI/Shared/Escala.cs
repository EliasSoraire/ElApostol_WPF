using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace ElApostol_WPF.UI.Shared
{
    /// <summary>
    /// Escala todas las ventanas según el tamaño de la pantalla, para que en una notebook chica
    /// entre todo y en un monitor grande las letras no queden diminutas.
    /// Además permite un zoom manual (Ctrl + / Ctrl − / Ctrl 0) que se recuerda en esta PC.
    /// Una ventana nunca se escala más de lo que entra en la pantalla, así no se corta contenido.
    /// </summary>
    public static class Escala
    {
        // Tamaño de pantalla para el que se diseñó la app (factor 1)
        private const double AnchoReferencia = 1280;
        private const double AltoReferencia = 760;
        private const double FactorMin = 0.75;
        private const double FactorMax = 1.3;

        private const double ZoomMin = 0.7;
        private const double ZoomMax = 1.5;
        private const double ZoomPaso = 0.1;

        private static readonly string ArchivoZoom = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ElApostol", "zoom.txt");

        private static double _zoomUsuario = 1.0;

        // Tamaños originales (sin escalar) de cada ventana
        private static readonly ConditionalWeakTable<Window, Diseno> _disenos = new();

        private sealed class Diseno
        {
            public double Ancho, Alto, MinAncho, MinAlto;
            public bool AnchoAuto, AltoAuto;
            public double Factor = 1.0;
        }

        public static void Registrar()
        {
            _zoomUsuario = LeerZoom();
            EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent,
                new RoutedEventHandler((s, _) => { if (s is Window w) Aplicar(w, primeraVez: true); }));
            EventManager.RegisterClassHandler(typeof(Window), UIElement.PreviewKeyDownEvent,
                new KeyEventHandler(OnPreviewKeyDown));
        }

        /// <summary>Factor según la pantalla, sin el zoom del usuario.</summary>
        private static double FactorPantalla()
        {
            var area = SystemParameters.WorkArea;
            var f = Math.Min(area.Width / AnchoReferencia, area.Height / AltoReferencia);
            return Math.Clamp(f, FactorMin, FactorMax);
        }

        private static void Aplicar(Window w, bool primeraVez)
        {
            if (w.Content is not FrameworkElement raiz) return;

            if (!_disenos.TryGetValue(w, out var d))
            {
                if (!primeraVez) return;
                var auto = w.SizeToContent;
                d = new Diseno
                {
                    AnchoAuto = auto is SizeToContent.Width or SizeToContent.WidthAndHeight || double.IsNaN(w.Width),
                    AltoAuto = auto is SizeToContent.Height or SizeToContent.WidthAndHeight || double.IsNaN(w.Height),
                    MinAncho = w.MinWidth,
                    MinAlto = w.MinHeight
                };
                d.Ancho = d.AnchoAuto ? w.ActualWidth : w.Width;
                d.Alto = d.AltoAuto ? w.ActualHeight : w.Height;
                _disenos.Add(w, d);
            }
            else if (primeraVez) return;

            var area = SystemParameters.WorkArea;
            var f = FactorPantalla() * _zoomUsuario;

            // Que la ventana completa entre en la pantalla
            if (d.Ancho > 0) f = Math.Min(f, area.Width * 0.98 / d.Ancho);
            if (d.Alto > 0) f = Math.Min(f, area.Height * 0.97 / d.Alto);
            f = Math.Max(f, 0.5);

            var anterior = d.Factor;
            d.Factor = f;
            raiz.LayoutTransform = Math.Abs(f - 1) < 0.001 ? Transform.Identity : new ScaleTransform(f, f);

            if (w.WindowState != WindowState.Normal) return;

            // Con SizeToContent activo WPF ignora el Width/Height nuevo: se apaga mientras se ajusta
            var stc = w.SizeToContent;
            w.SizeToContent = SizeToContent.Manual;

            if (!d.AnchoAuto)
            {
                w.MinWidth = Math.Min(d.MinAncho * f, area.Width);
                w.Width = primeraVez
                    ? d.Ancho * f
                    : Math.Clamp(w.Width * f / anterior, w.MinWidth, area.Width);
            }
            if (!d.AltoAuto)
            {
                w.MinHeight = Math.Min(d.MinAlto * f, area.Height);
                w.Height = primeraVez
                    ? d.Alto * f
                    : Math.Clamp(w.Height * f / anterior, w.MinHeight, area.Height);
            }

            w.SizeToContent = stc;
            w.UpdateLayout();
            Posicionar(w, centrar: primeraVez && w.WindowStartupLocation != WindowStartupLocation.Manual);
        }

        private static void Posicionar(Window w, bool centrar)
        {
            var area = SystemParameters.WorkArea;
            double ancho = w.ActualWidth, alto = w.ActualHeight;

            if (centrar)
            {
                var owner = w.Owner;
                if (w.WindowStartupLocation == WindowStartupLocation.CenterOwner && owner != null &&
                    owner.WindowState == WindowState.Normal)
                {
                    w.Left = owner.Left + (owner.ActualWidth - ancho) / 2;
                    w.Top = owner.Top + (owner.ActualHeight - alto) / 2;
                }
                else
                {
                    w.Left = area.Left + (area.Width - ancho) / 2;
                    w.Top = area.Top + (area.Height - alto) / 2;
                }
            }

            // Nunca fuera de la pantalla
            w.Left = Math.Max(area.Left, Math.Min(w.Left, area.Right - ancho));
            w.Top = Math.Max(area.Top, Math.Min(w.Top, area.Bottom - alto));
        }

        private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return;

            double nuevo;
            switch (e.Key)
            {
                case Key.OemPlus:
                case Key.Add:
                    nuevo = _zoomUsuario + ZoomPaso; break;
                case Key.OemMinus:
                case Key.Subtract:
                    nuevo = _zoomUsuario - ZoomPaso; break;
                case Key.D0:
                case Key.NumPad0:
                    nuevo = 1.0; break;
                default:
                    return;
            }

            e.Handled = true;
            nuevo = Math.Round(Math.Clamp(nuevo, ZoomMin, ZoomMax), 2);
            if (Math.Abs(nuevo - _zoomUsuario) < 0.001) return;

            _zoomUsuario = nuevo;
            GuardarZoom();
            foreach (Window w in Application.Current.Windows)
                Aplicar(w, primeraVez: false);
        }

        private static double LeerZoom()
        {
            try
            {
                if (File.Exists(ArchivoZoom) &&
                    double.TryParse(File.ReadAllText(ArchivoZoom), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var z))
                    return Math.Clamp(z, ZoomMin, ZoomMax);
            }
            catch { /* Sin archivo o sin permisos: zoom normal */ }
            return 1.0;
        }

        private static void GuardarZoom()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ArchivoZoom)!);
                File.WriteAllText(ArchivoZoom, _zoomUsuario.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            catch { /* No es crítico: solo se pierde la preferencia */ }
        }
    }
}
