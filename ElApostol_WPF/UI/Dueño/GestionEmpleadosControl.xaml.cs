using ElApostol_WPF.Business;
using ElApostol_WPF.Data;
using ElApostol_WPF.Models;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ElApostol_WPF.UI.Dueño
{
    public partial class GestionEmpleadosControl : UserControl
    {
        private Usuario _usuario = null!;
        private readonly UsuarioRepository _repo = new();
        private readonly VentaRepository _ventaRepo = new();

        private List<Usuario> _empleados = new();
        private Usuario? _empleadoSeleccionado;

        public GestionEmpleadosControl()
        {
            InitializeComponent();
        }

        public void Inicializar(Usuario usuario)
        {
            _usuario = usuario;
            _empleadoSeleccionado = null;
            PanelDetalle.Visibility = Visibility.Collapsed;
            CargarEmpleados();
        }

        private void CargarEmpleados()
        {
            if (PanelEmpleados == null) return;

            var busqueda = TxtBusqueda.Text.Trim().ToLower();
            var filtro = (CmbFiltroEstado.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Todos";

            _empleados = _repo.ObtenerEmpleados();

            var filtrados = _empleados.Where(e =>
            {
                var matchBusq = string.IsNullOrEmpty(busqueda)
                    || e.NombreCompleto.ToLower().Contains(busqueda)
                    || e.NombreUsuario.ToLower().Contains(busqueda);

                var matchFiltro = filtro switch
                {
                    "Activos" => e.Activo,
                    "Inactivos" => !e.Activo,
                    _ => true
                };

                return matchBusq && matchFiltro;
            }).ToList();

            RenderizarLista(filtrados);
        }

        private void RenderizarLista(List<Usuario> empleados)
        {
            if (PanelEmpleados == null) return;
            PanelEmpleados.Children.Clear();

            foreach (var emp in empleados)
            {
                var card = CrearTarjetaEmpleado(emp);
                PanelEmpleados.Children.Add(card);
            }
        }

        private Border CrearTarjetaEmpleado(Usuario emp)
        {
            var esSeleccionado = _empleadoSeleccionado?.Id == emp.Id;

            var card = new Border
            {
                Background = esSeleccionado
                    ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EAF3DE"))
                    : new SolidColorBrush(Colors.White),
                CornerRadius = new CornerRadius(8),
                BorderBrush = esSeleccionado
                    ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E7D32"))
                    : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EEEEEE")),
                BorderThickness = new Thickness(0.5),
                Padding = new Thickness(14, 12, 14, 12),
                Margin = new Thickness(0, 0, 0, 8),
                Cursor = System.Windows.Input.Cursors.Hand,
                Tag = emp
            };

            var avatarBg = emp.Activo ? "#EAF3DE" : "#F5F5F5";
            var avatarFg = emp.Activo ? "#27500A" : "#AAAAAA";
            var avatarBorder = new Border
            {
                Width = 40,
                Height = 40,
                CornerRadius = new CornerRadius(20),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(avatarBg))
            };
            avatarBorder.Child = new TextBlock
            {
                Text = ObtenerIniciales(emp.NombreCompleto),
                FontSize = 14,
                FontWeight = FontWeights.Medium,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(avatarFg)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var infoPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            infoPanel.Children.Add(new TextBlock
            {
                Text = emp.NombreCompleto,
                FontSize = 13,
                FontWeight = FontWeights.Medium,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(
                    esSeleccionado ? "#1B5E20" : "#1A1A1A"))
            });
            infoPanel.Children.Add(new TextBlock
            {
                Text = $"@{emp.NombreUsuario} · Alta: {emp.FechaAlta:dd/MM/yyyy}",
                FontSize = 11,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#666666")),
                Margin = new Thickness(0, 2, 0, 0)
            });

            var badgeBg = emp.Activo ? "#EAF3DE" : "#F5F5F5";
            var badgeFg = emp.Activo ? "#27500A" : "#AAAAAA";
            var badge = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(badgeBg)),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(8, 2, 8, 2)
            };
            badge.Child = new TextBlock
            {
                Text = emp.Activo ? "Activo" : "Inactivo",
                FontSize = 11,
                FontWeight = FontWeights.Medium,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(badgeFg))
            };

            var ventasHoy = _ventaRepo.ObtenerVentasHoyEmpleado(emp.Id);
            var ventasText = new TextBlock
            {
                Text = ventasHoy > 0 ? $"{ventasHoy} ventas hoy" : "— ventas hoy",
                FontSize = 11,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#AAAAAA")),
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var statsPanel = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            statsPanel.Children.Add(badge);
            statsPanel.Children.Add(ventasText);

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Grid.SetColumn(avatarBorder, 0);
            Grid.SetColumn(infoPanel, 1);
            Grid.SetColumn(statsPanel, 2);

            grid.Children.Add(avatarBorder);

            var infoWrapper = new Border { Margin = new Thickness(12, 0, 12, 0) };
            infoWrapper.Child = infoPanel;
            Grid.SetColumn(infoWrapper, 1);
            grid.Children.Add(infoWrapper);
            grid.Children.Add(statsPanel);

            card.Child = grid;

            card.MouseLeftButtonUp += (_, _) => SeleccionarEmpleado(emp);

            return card;
        }

        private void SeleccionarEmpleado(Usuario emp)
        {
            _empleadoSeleccionado = emp;
            CargarEmpleados();

            MostrarPanelDetalle();

            TxtPanelAvatar.Text = ObtenerIniciales(emp.NombreCompleto);
            TxtPanelNombre.Text = emp.NombreCompleto;
            TxtPanelRol.Text = $"Empleado · {(emp.Activo ? "Activo" : "Inactivo")}";
            TxtPanelUsuario.Text = $"@{emp.NombreUsuario}";
            TxtPanelFechaAlta.Text = emp.FechaAlta.ToString("dd/MM/yyyy");

            var ventasHoy = _ventaRepo.ObtenerVentasHoyEmpleado(emp.Id);
            var totalHoy = _ventaRepo.ObtenerTotalHoyEmpleado(emp.Id);
            TxtPanelVentasHoy.Text = $"{ventasHoy} transacciones";
            TxtPanelTotalHoy.Text = $"${totalHoy:N0}";

            BtnToggleActivo.Content = emp.Activo ? "Desactivar empleado" : "Activar empleado";
        }

        private void MostrarPanelDetalle()
        {
            PanelDetalle.Visibility = Visibility.Visible;
            ScrollDetalle.Visibility = Visibility.Visible;
            ScrollNuevo.Visibility = Visibility.Collapsed;
            AccionesDetalle.Visibility = Visibility.Visible;
            AccionesNuevo.Visibility = Visibility.Collapsed;
            TxtPanelTitulo.Text = "Detalle del empleado";
        }

        private void MostrarPanelNuevo()
        {
            _empleadoSeleccionado = null;
            PanelDetalle.Visibility = Visibility.Visible;
            ScrollDetalle.Visibility = Visibility.Collapsed;
            ScrollNuevo.Visibility = Visibility.Visible;
            AccionesDetalle.Visibility = Visibility.Collapsed;
            AccionesNuevo.Visibility = Visibility.Visible;
            TxtPanelTitulo.Text = "Nuevo empleado";
            TxtNuevoNombre.Text = string.Empty;
            TxtNuevoUsuario.Text = string.Empty;
            TxtNuevoPass.Text = string.Empty;
            CargarEmpleados();
        }

        private void BtnNuevoEmpleado_Click(object sender, RoutedEventArgs e) => MostrarPanelNuevo();

        private void BtnCerrarPanel_Click(object sender, RoutedEventArgs e)
        {
            _empleadoSeleccionado = null;
            PanelDetalle.Visibility = Visibility.Collapsed;
            CargarEmpleados();
        }

        private void BtnGenerarPass_Click(object sender, RoutedEventArgs e)
        {
            TxtNuevoPass.Text = GenerarContrasenaAleatoria();
        }

        private void BtnCrearEmpleado_Click(object sender, RoutedEventArgs e)
        {
            var nombre = TxtNuevoNombre.Text.Trim();
            var usuario = TxtNuevoUsuario.Text.Trim();
            var pass = TxtNuevoPass.Text.Trim();

            if (string.IsNullOrEmpty(nombre) || string.IsNullOrEmpty(usuario) || string.IsNullOrEmpty(pass))
            {
                MessageBox.Show("Completá todos los campos.", "Campos requeridos",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                if (_repo.ExisteNombreUsuario(usuario))
                {
                    MessageBox.Show($"Ya existe un usuario \"@{usuario}\". Elegí otro nombre de usuario.",
                        "Usuario repetido", MessageBoxButton.OK, MessageBoxImage.Warning);
                    TxtNuevoUsuario.Focus();
                    return;
                }

                var hash = BCrypt.Net.BCrypt.HashPassword(pass);

                _repo.CrearEmpleado(nombre, usuario, hash);

                PanelDetalle.Visibility = Visibility.Collapsed;
                CargarEmpleados();

                MessageBox.Show($"Empleado \"{nombre}\" creado correctamente.",
                    "Empleado creado", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number is 2627 or 2601)
            {
                MessageBox.Show($"Ya existe un usuario \"@{usuario}\". Elegí otro nombre de usuario.",
                    "Usuario repetido", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al crear el empleado:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCancelarNuevo_Click(object sender, RoutedEventArgs e)
        {
            PanelDetalle.Visibility = Visibility.Collapsed;
            _empleadoSeleccionado = null;
            CargarEmpleados();
        }

        private void BtnResetearContrasena_Click(object sender, RoutedEventArgs e)
        {
            if (_empleadoSeleccionado == null) return;

            var nuevaPass = GenerarContrasenaAleatoria();

            try
            {
                var auth = new AuthService();
                if (!auth.CambiarContrasena(_empleadoSeleccionado.Id, nuevaPass, esTemporal: true))
                {
                    MessageBox.Show("No se pudo resetear la contraseña. Verificá la conexión e intentá de nuevo.",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                MessageBox.Show(
                    $"Contraseña de {_empleadoSeleccionado.NombreCompleto} reseteada.\n\nNueva contraseña temporal:\n{nuevaPass}\n\nComunicala al empleado.",
                    "Contraseña reseteada",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al resetear:\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void BtnToggleActivo_Click(object sender, RoutedEventArgs e)
        {
            if (_empleadoSeleccionado == null) return;

            var accion = _empleadoSeleccionado.Activo ? "desactivar" : "activar";
            var result = MessageBox.Show(
                $"¿Querés {accion} a {_empleadoSeleccionado.NombreCompleto}?",
                "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            _repo.CambiarEstadoEmpleado(_empleadoSeleccionado.Id, !_empleadoSeleccionado.Activo);
            _empleadoSeleccionado.Activo = !_empleadoSeleccionado.Activo;
            SeleccionarEmpleado(_empleadoSeleccionado);
        }

        private void TxtBusqueda_TextChanged(object sender, TextChangedEventArgs e) => CargarEmpleados();
        private void CmbFiltroEstado_SelectionChanged(object sender, SelectionChangedEventArgs e) => CargarEmpleados();

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
