using ElApostol_WPF.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;

namespace ElApostol_WPF.UI.Shared
{
    public partial class VentaConfirmacionWindow : Window
    {
        private readonly Venta _venta;
        private readonly string _nombreEmpleado;
        private List<CarritoItem> _items = new();
        private decimal _recargoPorc;

        public VentaConfirmacionWindow(Venta venta, string nombreEmpleado,
                                       List<CarritoItem> items, decimal recargoPorc)
        {
            InitializeComponent();
            _venta = venta;
            _nombreEmpleado = nombreEmpleado;
            _items = items;
            _recargoPorc = recargoPorc;
            CargarDatos(items, recargoPorc);
        }

        private void CargarDatos(List<CarritoItem> items, decimal recargoPorc)
        {
            var cultura = new CultureInfo("es-AR");
            var fecha = _venta.FechaHora;
            var fechaTxt = fecha.ToString("dddd dd/MM/yyyy", cultura);

            TxtNumeroComprobante.Text = $"Comprobante #{_venta.NumeroComprobante}";
            TxtNumComp2.Text = $"#{_venta.NumeroComprobante}";
            TxtFecha.Text = char.ToUpper(fechaTxt[0]) + fechaTxt[1..];
            TxtHora.Text = fecha.ToString("HH:mm") + " hs";
            RunEmpleado.Text = _nombreEmpleado;
            IcItems.ItemsSource = items;
            TxtConfSubtotal.Text = $"${_venta.Subtotal:N0}";
            TxtConfTotal.Text = $"${_venta.Total:N0}";

            if (_venta.MontoRecargo > 0)
            {
                FilaRecargoConf.Visibility = Visibility.Visible;
                TxtLabelRecargoConf.Text = $"Recargo tarjeta ({recargoPorc * 100:0}%)";
                TxtConfRecargo.Text = $"+${_venta.MontoRecargo:N0}";
            }

            TxtMetodoBadge.Text = _venta.MetodoPago switch
            {
                "efectivo" => "Efectivo",
                "tarjeta" => "Tarjeta de crédito",
                "transferencia" => "Transferencia bancaria",
                _ => _venta.MetodoPago
            };

            IconoMetodo.Data = System.Windows.Media.Geometry.Parse(
                _venta.MetodoPago switch
                {
                    "tarjeta" => "M1,4 H15 V13 Q15,14 14,14 H2 Q1,14 1,13 Z M1,7 H15",
                    "transferencia" => "M2,5 H14 M11,2 L14,5 L11,8 M14,11 H2 M5,8 L2,11 L5,14",
                    _ => "M1,4 H15 V12 H1 Z M8,6 A2,2 0 1 0 8,10 A2,2 0 1 0 8,6"
                });

            TxtObsConf.Text = string.IsNullOrWhiteSpace(_venta.Observaciones)
                ? "Sin observaciones"
                : _venta.Observaciones;
        }

        private void BtnConfirmar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BtnPdf_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var rutaPdf = GenerarRutaPdf();
                Directory.CreateDirectory(Path.GetDirectoryName(rutaPdf)!);

                GenerarPdf(rutaPdf);

                var res = MessageBox.Show(
                    $"Comprobante guardado en:\n{rutaPdf}\n\n¿Desea abrirlo ahora?",
                    "PDF guardado",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (res == MessageBoxResult.Yes)
                    Process.Start(new ProcessStartInfo(rutaPdf) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error al generar el PDF:\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private string GenerarRutaPdf()
        {
            var carpeta = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Comprobantes");

            var fecha = _venta.FechaHora.ToString("dd-MM-yyyy");
            var empleado = _nombreEmpleado.Replace(" ", "_").Replace("/", "-");
            var nombre = $"{_venta.NumeroComprobante}_{fecha}_{empleado}.pdf";

            foreach (var c in Path.GetInvalidFileNameChars())
                nombre = nombre.Replace(c, '-');

            return Path.Combine(carpeta, nombre);
        }

        private void GenerarPdf(string rutaDestino)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var cultura = new CultureInfo("es-AR");
            var fecha = _venta.FechaHora;
            var fechaTxt = fecha.ToString("dddd dd/MM/yyyy", cultura);
            fechaTxt = char.ToUpper(fechaTxt[0]) + fechaTxt[1..];

            const string Verde = "#1B5E20";
            const string VerdeMedio = "#2E7D32";
            const string VerdeBg = "#EAF3DE";
            const string Gris1 = "#111111";
            const string Gris2 = "#444444";
            const string Gris3 = "#888888";
            const string LineaBg = "#F8FAF6";

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(0);
                    page.DefaultTextStyle(ts =>
                        ts.FontFamily("Arial").FontSize(10).FontColor(Gris1));

                    page.Content().Column(col =>
                    {
                        col.Item().Background(Verde).Padding(28).Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("El Apóstol")
                                    .FontSize(22).Bold().FontColor("#FFFFFF");
                                c.Item().Text("Materiales de construcción")
                                    .FontSize(11).FontColor("#FFFFFFCC");
                            });

                            row.AutoItem().AlignRight().Column(c =>
                            {
                                c.Item().Text("COMPROBANTE DE VENTA")
                                    .FontSize(11).Bold().FontColor("#FFFFFFCC").AlignRight();
                                c.Item().Text($"#{_venta.NumeroComprobante}")
                                    .FontSize(18).Bold().FontColor("#FFFFFF").AlignRight();
                                c.Item().PaddingTop(4)
                                    .Text($"{fechaTxt}  —  {fecha:HH:mm} hs")
                                    .FontSize(10).FontColor("#FFFFFFCC").AlignRight();
                            });
                        });

                        col.Item().Padding(28).Column(body =>
                        {
                            body.Item().PaddingBottom(16)
                                .Background(VerdeBg)
                                .Border(0.5f).BorderColor("#B8D89A")
                                .PaddingVertical(6)
                                .PaddingHorizontal(9)
                                .Text(text =>
                                {
                                    text.Span("Atendido por:   ")
                                        .FontSize(10).FontColor(Gris2);
                                    text.Span(_nombreEmpleado)
                                        .FontSize(10).Bold().FontColor(Verde);
                                });

                            body.Item().Table(table =>
                            {
                                table.ColumnsDefinition(cols =>
                                {
                                    cols.ConstantColumn(68);    
                                    cols.RelativeColumn(3);     
                                    cols.RelativeColumn(1.4f);  
                                    cols.ConstantColumn(42);    
                                    cols.ConstantColumn(72);    
                                    cols.ConstantColumn(78);    
                                });

                                static IContainer HCell(IContainer c) =>
                                    c.Background("#1B5E20")
                                    .PaddingVertical(6)
                                    .PaddingHorizontal(7);     

                                table.Header(h =>
                                {
                                    h.Cell().Element(HCell).Text("Código")
                                        .FontSize(9.5f).Bold().FontColor("#FFFFFF");
                                    h.Cell().Element(HCell).Text("Material")
                                        .FontSize(9.5f).Bold().FontColor("#FFFFFF");
                                    h.Cell().Element(HCell).Text("Medida")
                                        .FontSize(9.5f).Bold().FontColor("#FFFFFF");
                                    h.Cell().Element(HCell).Text("Cant.")
                                        .FontSize(9.5f).Bold().FontColor("#FFFFFF").AlignCenter();
                                    h.Cell().Element(HCell).Text("P. Unit.")
                                        .FontSize(9.5f).Bold().FontColor("#FFFFFF").AlignRight();
                                    h.Cell().Element(HCell).Text("Subtotal")
                                        .FontSize(9.5f).Bold().FontColor("#FFFFFF").AlignRight();
                                });

                                bool impar = true;
                                foreach (var item in _items)
                                {
                                    var bg = impar ? "#FFFFFF" : LineaBg;
                                    impar = !impar;

                                    IContainer DCell(IContainer c) =>
                                        c.Background(bg)
                                         .BorderBottom(0.5f).BorderColor("#E8E8E8")
                                         .PaddingVertical(6)
                                         .PaddingHorizontal(7);

                                    table.Cell().Element(DCell)
                                        .Text(item.CodigoInterno).FontSize(9.5f).FontColor(Gris2);
                                    table.Cell().Element(DCell)
                                        .Text(item.NombreProducto).FontSize(9.5f).FontColor(Gris1);
                                    table.Cell().Element(DCell)
                                        .Text(item.Medida).FontSize(9.5f).FontColor(Gris2);
                                    table.Cell().Element(DCell)
                                        .Text(item.Cantidad.ToString())
                                        .FontSize(9.5f).AlignCenter();
                                    table.Cell().Element(DCell)
                                        .Text(item.PrecioDisplay).FontSize(9.5f).AlignRight();
                                    table.Cell().Element(DCell)
                                        .Text(item.SubtotalDisplay)
                                        .FontSize(9.5f).Bold().FontColor(VerdeMedio).AlignRight();
                                }
                            });

                            body.Item().PaddingTop(12).AlignRight().Width(220).Column(tot =>
                            {
                                tot.Item()
                                    .BorderBottom(0.5f).BorderColor("#E0E0E0")
                                    .PaddingBottom(5).Row(row =>
                                    {
                                        row.RelativeItem()
                                            .Text("Subtotal").FontSize(10).FontColor(Gris2);
                                        row.AutoItem()
                                            .Text($"${_venta.Subtotal:N0}").FontSize(10).Bold();
                                    });

                                if (_venta.MontoRecargo > 0)
                                {
                                    tot.Item()
                                        .PaddingTop(5)
                                        .BorderBottom(0.5f).BorderColor("#E0E0E0")
                                        .PaddingBottom(5).Row(row =>
                                        {
                                            row.RelativeItem()
                                                .Text($"Recargo tarjeta ({_recargoPorc * 100:0}%)")
                                                .FontSize(10).FontColor("#7B4500");
                                            row.AutoItem()
                                                .Text($"+${_venta.MontoRecargo:N0}")
                                                .FontSize(10).Bold().FontColor("#7B4500");
                                        });
                                }

                                tot.Item()
                                    .Background(Verde)
                                    .PaddingVertical(7)
                                    .PaddingHorizontal(8)
                                    .Row(row =>
                                    {
                                    row.RelativeItem()
                                        .Text("TOTAL").FontSize(13).Bold().FontColor("#FFFFFF");
                                    row.AutoItem()
                                        .Text($"${_venta.Total:N0}")
                                        .FontSize(13).Bold().FontColor("#FFFFFF");
                                });
                            });

                            body.Item().PaddingTop(20).Row(row =>
                            {
                                row.RelativeItem().Column(c =>
                                {
                                    c.Item().Text("Método de pago")
                                        .FontSize(9).FontColor(Gris3);
                                    c.Item().Text(
                                        _venta.MetodoPago switch
                                        {
                                            "efectivo" => "Efectivo",
                                            "tarjeta" => "Tarjeta de crédito",
                                            "transferencia" => "Transferencia bancaria",
                                            _ => _venta.MetodoPago
                                        })
                                        .FontSize(11).Bold().FontColor(Verde);
                                });

                                if (!string.IsNullOrWhiteSpace(_venta.Observaciones))
                                {
                                    row.RelativeItem().AlignRight().Column(c =>
                                    {
                                        c.Item().Text("Observaciones")
                                            .FontSize(9).FontColor(Gris3).AlignRight();
                                        c.Item().Text(_venta.Observaciones)
                                            .FontSize(10).Italic().FontColor(Gris2).AlignRight();
                                    });
                                }
                            });
                        });

                        col.Item()
                            .Background("#F5F5F5")
                            .BorderTop(0.5f).BorderColor("#DDDDDD")
                            .PaddingVertical(10)
                            .PaddingHorizontal(20)
                            .Row(row =>
                            {
                                row.RelativeItem()
                                    .Text("El Apóstol — Materiales de Construcción")
                                    .FontSize(9).FontColor(Gris3);
                                row.AutoItem()
                                    .Text($"Documento generado el {fecha:dd/MM/yyyy} a las {fecha:HH:mm} hs   ·   Gracias por su compra")
                                    .FontSize(9).FontColor(Gris3).AlignRight();
                            });
                    });
                });
            }).GeneratePdf(rutaDestino);
        }
    }
}