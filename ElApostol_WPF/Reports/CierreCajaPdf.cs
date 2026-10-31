using ElApostol_WPF.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;

namespace ElApostol_WPF.Reports
{
    public class CierreCajaData
    {
        public string Empleado { get; set; } = "";
        public string Usuario { get; set; } = "";
        public string Periodo { get; set; } = "";
        public DateTime FechaDesde { get; set; }
        public DateTime FechaHasta { get; set; }
        public DateTime FechaGeneracion { get; set; } = DateTime.Now;
        public List<VentaDetallada> Ventas { get; set; } = new();
    }

    /// <summary>Cierre de caja de un empleado: totales por método de pago y ventas del período.</summary>
    public class CierreCajaPdf : IDocument
    {
        private const string Verde = "#2E7D32";
        private const string VerdeOsc = "#1B5E20";
        private const string TextGris = "#666666";
        private const string BorderGris = "#DDDDDD";
        private const string BgGris = "#F5F5F5";

        private readonly CierreCajaData _data;
        private readonly CultureInfo _ci = new("es-AR");

        public CierreCajaPdf(CierreCajaData data) => _data = data;

        public static void Generate(string rutaArchivo, CierreCajaData data)
        {
            QuestPDF.Settings.License = LicenseType.Community;
            new CierreCajaPdf(data).GeneratePdf(rutaArchivo);
        }

        public DocumentMetadata GetMetadata() => new()
        {
            Title = "Cierre de caja — El Apóstol",
            Author = _data.Empleado,
            CreationDate = _data.FechaGeneracion
        };

        public DocumentSettings GetSettings() => DocumentSettings.Default;

        private string Moneda(decimal v) => v.ToString("C0", _ci);

        private decimal TotalMetodo(string metodo) =>
            _data.Ventas.Where(v => v.MetodoPago == metodo).Sum(v => v.Total);

        private int CantidadMetodo(string metodo) =>
            _data.Ventas.Count(v => v.MetodoPago == metodo);

        public void Compose(IDocumentContainer container)
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(t => t.FontFamily("Arial").FontSize(9).FontColor("#1A1A1A"));

                page.Header().Element(ComposeHeader);
                page.Content().Element(ComposeContent);
                page.Footer().Element(ComposeFooter);
            });
        }

        private void ComposeHeader(IContainer c)
        {
            c.Column(col =>
            {
                col.Item().Background(VerdeOsc).Padding(16).Row(row =>
                {
                    row.RelativeItem().Column(inner =>
                    {
                        inner.Item().Text("El Apóstol").FontSize(18).Bold().FontColor("#FFFFFF");
                        inner.Item().Text("Materiales de construcción").FontSize(10).FontColor("#CCFFFFFF");
                    });

                    row.AutoItem().AlignRight().AlignMiddle().Column(inner =>
                    {
                        inner.Item().Text("CIERRE DE CAJA").FontSize(11).Bold().FontColor("#FFFFFF").AlignRight();
                        inner.Item().Text(_data.Periodo).FontSize(9).FontColor("#CCFFFFFF").AlignRight();
                        inner.Item().Text($"Empleado: {_data.Empleado} (@{_data.Usuario})")
                             .FontSize(9).FontColor("#CCFFFFFF").AlignRight();
                        inner.Item().Text($"Emitido: {_data.FechaGeneracion:dd/MM/yyyy HH:mm}")
                             .FontSize(9).FontColor("#CCFFFFFF").AlignRight();
                    });
                });

                col.Item().Height(12);
            });
        }

        private void ComposeContent(IContainer c)
        {
            var total = _data.Ventas.Sum(v => v.Total);
            var recargos = _data.Ventas.Sum(v => v.MontoRecargo);
            var unidades = _data.Ventas.Sum(v => v.Unidades);

            c.Column(col =>
            {
                // Resumen general
                col.Item().Text("RESUMEN").FontSize(9).Bold().FontColor(TextGris).LetterSpacing(0.05f);
                col.Item().Height(6);
                col.Item().Row(row =>
                {
                    Card(row.RelativeItem(), "Total recaudado", Moneda(total), Verde, 18);
                    row.ConstantItem(8);
                    Card(row.RelativeItem(), "Ventas", _data.Ventas.Count.ToString(), "#1A1A1A", 18);
                    row.ConstantItem(8);
                    Card(row.RelativeItem(), "Unidades vendidas", unidades.ToString(), "#1A1A1A", 18);
                    row.ConstantItem(8);
                    Card(row.RelativeItem(), "Recargos tarjeta", Moneda(recargos), "#854F0B", 18);
                });

                col.Item().Height(16);

                // Arqueo por método de pago
                col.Item().Text("ARQUEO POR MÉTODO DE PAGO").FontSize(9).Bold().FontColor(TextGris).LetterSpacing(0.05f);
                col.Item().Height(6);
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(3);
                        cols.RelativeColumn(1.5f);
                        cols.RelativeColumn(2);
                    });

                    Encabezado(table, "Método", "Operaciones", "Total");

                    foreach (var (metodo, label) in new[] { ("efectivo", "Efectivo"), ("transferencia", "Transferencia"), ("tarjeta", "Tarjeta (con recargo)") })
                    {
                        IContainer Celda(IContainer x) => x.BorderBottom(0.5f).BorderColor("#F0F0F0").PaddingVertical(7).PaddingHorizontal(8);
                        table.Cell().Element(Celda).Text(label).Bold();
                        table.Cell().Element(Celda).Text(CantidadMetodo(metodo).ToString()).FontColor(TextGris);
                        table.Cell().Element(Celda).Text(Moneda(TotalMetodo(metodo))).Bold().FontColor(Verde);
                    }

                    IContainer Total(IContainer x) => x.Background(BgGris).BorderTop(1f).BorderColor(Verde).PaddingVertical(7).PaddingHorizontal(8);
                    table.Cell().Element(Total).Text("TOTAL").Bold().FontColor(VerdeOsc);
                    table.Cell().Element(Total).Text(_data.Ventas.Count.ToString()).Bold();
                    table.Cell().Element(Total).Text(Moneda(total)).Bold().FontColor(Verde).FontSize(10);
                });

                col.Item().Height(16);

                // Detalle de ventas
                col.Item().Text($"VENTAS DEL PERÍODO ({_data.Ventas.Count})").FontSize(9).Bold().FontColor(TextGris).LetterSpacing(0.05f);
                col.Item().Height(6);

                if (_data.Ventas.Count == 0)
                {
                    col.Item().Border(0.5f).BorderColor(BorderGris).Padding(14)
                       .AlignCenter().Text("No se registraron ventas en este período.").FontColor(TextGris);
                }
                else
                {
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(1.5f); // Comprobante
                            cols.RelativeColumn(1.6f); // Fecha y hora
                            cols.RelativeColumn(4f);   // Materiales
                            cols.RelativeColumn(1.5f); // Método
                            cols.RelativeColumn(1.5f); // Total
                        });

                        Encabezado(table, "Comprobante", "Fecha", "Materiales", "Método", "Total");

                        var alt = false;
                        foreach (var v in _data.Ventas.OrderBy(v => v.FechaHora))
                        {
                            var bg = alt ? "#FAFAFA" : "#FFFFFF";
                            alt = !alt;
                            IContainer Celda(IContainer x) => x.Background(bg).BorderBottom(0.5f).BorderColor("#F0F0F0").PaddingVertical(5).PaddingHorizontal(6);

                            table.Cell().Element(Celda).Text(v.NumeroComprobante).FontSize(8.5f);
                            table.Cell().Element(Celda).Text(v.FechaHora.ToString("dd/MM HH:mm")).FontSize(8.5f).FontColor(TextGris);
                            table.Cell().Element(Celda).Column(items =>
                            {
                                foreach (var l in v.Lineas)
                                {
                                    var medida = string.IsNullOrEmpty(l.Medida) ? "" : $" · {l.Medida}";
                                    items.Item().Text($"{l.Cantidad} × {l.NombreProducto}{medida}").FontSize(8);
                                }
                            });
                            table.Cell().Element(Celda).Text(v.MetodoPagoDisplay).FontSize(8.5f).FontColor(TextGris);
                            table.Cell().Element(Celda).Text(Moneda(v.Total)).Bold().FontColor(Verde).FontSize(8.5f);
                        }
                    });
                }
            });
        }

        private static void Card(IContainer c, string titulo, string valor, string color, float tam)
        {
            c.Border(0.5f).BorderColor(BorderGris).CornerRadius(6).Padding(12).Column(col =>
            {
                col.Item().Text(titulo).FontSize(9).FontColor(TextGris);
                col.Item().Height(4);
                col.Item().Text(valor).FontSize(tam).Bold().FontColor(color);
            });
        }

        private static void Encabezado(TableDescriptor table, params string[] headers)
        {
            table.Header(hdr =>
            {
                foreach (var h in headers)
                    hdr.Cell().Background(Verde).PaddingVertical(6).PaddingHorizontal(8)
                       .Text(h).Bold().FontSize(9).FontColor("#FFFFFF");
            });
        }

        private void ComposeFooter(IContainer c)
        {
            c.BorderTop(0.5f).BorderColor(BorderGris).PaddingTop(8).Row(row =>
            {
                row.RelativeItem()
                   .Text($"El Apóstol — Cierre de caja de {_data.Empleado} | Generado el {_data.FechaGeneracion:dd/MM/yyyy HH:mm}")
                   .FontSize(7).FontColor("#AAAAAA");
                row.AutoItem().Text(t =>
                {
                    t.DefaultTextStyle(s => s.FontSize(7).FontColor("#AAAAAA"));
                    t.Span("Página ");
                    t.CurrentPageNumber();
                    t.Span(" de ");
                    t.TotalPages();
                });
            });
        }
    }
}
