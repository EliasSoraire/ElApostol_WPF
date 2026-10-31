using System.Configuration;
using System.Data;
using System.Windows;

namespace ElApostol_WPF
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // Escala automática de todas las ventanas según la pantalla + zoom con Ctrl +/−
            UI.Shared.Escala.Registrar();
            base.OnStartup(e);
        }
    }

}
