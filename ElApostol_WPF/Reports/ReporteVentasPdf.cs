using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ElApostol_WPF.Reports
{
    public class ReporteVentasData
    {
        public string Administrador { get; set; } = "";
        public DateTime FechaDesde { get; set; }
        public DateTime FechaHasta { get; set; }
        public DateTime FechaGeneracion { get; set; } = DateTime.Now;

        public decimal TotalIngresos { get; set; }
        public int TotalTransacciones { get; set; }
        public decimal TicketPromedio { get; set; }
        public string MejorDia { get; set; } = "—";

        public List<FilaDiaDto> Filas { get; set; } = new();

        public decimal TotalEfectivo { get; set; }
        public decimal TotalTarjeta { get; set; }
        public decimal TotalTransferencia { get; set; }
    }

    public class FilaDiaDto
    {
        public string DiaNombre { get; set; } = "";
        public DateTime Fecha { get; set; }
        public int CantidadVentas { get; set; }
        public decimal Total { get; set; }
        public decimal TicketPromedio { get; set; }
        public string Empleado { get; set; } = "—";
        public bool EsMejorDia { get; set; }
    }

    public class ReporteVentasPdf : IDocument
    {
        private static readonly string Verde = "#2E7D32";
        private static readonly string VerdeOsc = "#1B5E20";
        private static readonly string VerdeLight = "#EAF3DE";
        private static readonly string TextGris = "#666666";
        private static readonly string BorderGris = "#DDDDDD";
        private static readonly string BgGris = "#F5F5F5";

        private readonly ReporteVentasData _data;
        private readonly CultureInfo _ci = new("es-AR");

        public ReporteVentasPdf(ReporteVentasData data) => _data = data;

        public static void Generate(string rutaArchivo, ReporteVentasData data)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var doc = new ReporteVentasPdf(data);
            doc.GeneratePdf(rutaArchivo);
        }

        public DocumentMetadata GetMetadata() => new()
        {
            Title = "Reporte Semanal de Ventas — El Apóstol",
            Author = _data.Administrador,
            CreationDate = _data.FechaGeneracion
        };

        public DocumentSettings GetSettings() => DocumentSettings.Default;

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
                        inner.Item()
                             .Text("El Apóstol")
                             .FontSize(18).Bold().FontColor("#FFFFFF");
                        inner.Item()
                             .Text("Materiales de construcción")
                             .FontSize(10).FontColor("#CCFFFFFF");
                    });

                    row.AutoItem().AlignRight().AlignMiddle().Column(inner =>
                    {
                        inner.Item()
                             .Text("REPORTE SEMANAL DE VENTAS")
                             .FontSize(11).Bold().FontColor("#FFFFFF").AlignRight();
                        inner.Item()
                             .Text($"Semana: {_data.FechaDesde:dd/MM/yyyy} al {_data.FechaHasta:dd/MM/yyyy}")
                             .FontSize(9).FontColor("#CCFFFFFF").AlignRight();
                        inner.Item()
                             .Text($"Generado el {_data.FechaGeneracion:dddd dd/MM/yyyy}")
                             .FontSize(9).FontColor("#CCFFFFFF").AlignRight();
                        inner.Item()
                             .Text($"Administrador: {_data.Administrador}")
                             .FontSize(9).FontColor("#CCFFFFFF").AlignRight();
                    });
                });

                col.Item().Height(12);
            });
        }

        private void ComposeContent(IContainer c)
        {
            c.Column(col =>
            {
                col.Item().Text("MÉTRICAS DE LA SEMANA")
                          .FontSize(9).Bold().FontColor(TextGris).LetterSpacing(0.05f);

                col.Item().Height(6);

                col.Item().Row(row =>
                {
                    MetricaCard(row.RelativeItem(), "Total ingresos",
                        _data.TotalIngresos.ToString("C0", _ci), Verde, true);
                    row.ConstantItem(8);
                    MetricaCard(row.RelativeItem(), "Transacciones",
                        _data.TotalTransacciones.ToString(), "#1A1A1A");
                    row.ConstantItem(8);
                    MetricaCard(row.RelativeItem(), "Ticket promedio",
                        _data.TicketPromedio.ToString("C0", _ci), "#1A1A1A");
                    row.ConstantItem(8);
                    MetricaCard(row.RelativeItem(), "Mejor día",
                        _data.MejorDia, "#1A1A1A");
                });

                col.Item().Height(16);

                col.Item().Text("VENTAS POR DÍA DE LA SEMANA")
                          .FontSize(9).Bold().FontColor(TextGris).LetterSpacing(0.05f);
                col.Item().Height(6);

                col.Item().Element(ComposeTabla);

                col.Item().Height(16);

                col.Item().Text("DESGLOSE POR MÉTODO DE PAGO")
                          .FontSize(9).Bold().FontColor(TextGris).LetterSpacing(0.05f);
                col.Item().Height(6);
                col.Item().Element(ComposeMetodosPago);
            });
        }

        private void MetricaCard(IContainer c, string titulo, string valor,
                                  string colorValor, bool destacar = false)
        {
            c.Border(0.5f).BorderColor(BorderGris).CornerRadius(6)
             .Padding(12).Column(col =>
             {
                 col.Item().Text(titulo).FontSize(9).FontColor(TextGris);
                 col.Item().Height(4);
                 col.Item().Text(valor).FontSize(18).Bold().FontColor(colorValor);
                 if (destacar)
                     col.Item().Text("Esta semana").FontSize(8).FontColor("#AAAAAA");
             });
        }

        private void ComposeTabla(IContainer c)
        {
            c.Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(2.2f); 
                    cols.RelativeColumn(1.8f); 
                    cols.RelativeColumn(1f);   
                    cols.RelativeColumn(2f);   
                    cols.RelativeColumn(2f);
                    cols.RelativeColumn(3f);
                });

                string[] headers = { "Día", "Fecha", "Ventas", "Total", "Ticket Prom.", "Empleado" };
                foreach (var h in headers)
                {
                    table.Header(hdr =>
                    {
                    });
                }

                TableHeader(table, headers);

                bool alt = false;
                foreach (var f in _data.Filas)
                {
                    var bg = alt ? "#FAFAFA" : "#FFFFFF";
                    alt = !alt;

                    table.Cell().Background(bg).BorderBottom(0.5f).BorderColor("#F0F0F0")
                         .PaddingVertical(6)
                         .PaddingHorizontal(8)
                         .Row(row =>
                         {   
                             row.AutoItem()
                                .Text(f.DiaNombre).Bold().FontSize(9);
                             if (f.EsMejorDia)
                             {
                                 row.ConstantItem(4);
                                 row.AutoItem()
                                    .Background(VerdeLight).CornerRadius(8)
                                    .PaddingHorizontal(5).PaddingVertical(1)
                                    .Text("mejor").FontSize(7).FontColor("#27500A");
                             }
                         });

                    table.Cell().Background(bg).BorderBottom(0.5f).BorderColor("#F0F0F0")
                         .PaddingVertical(6)
                         .PaddingHorizontal(8)
                         .Text(f.Fecha.ToString("dd/MM/yyyy")).FontColor(TextGris).FontSize(9);

                    table.Cell().Background(bg).BorderBottom(0.5f).BorderColor("#F0F0F0")
                         .PaddingVertical(6)
                         .PaddingHorizontal(8)
                         .Text(f.CantidadVentas.ToString()).FontSize(9);

                    table.Cell().Background(bg).BorderBottom(0.5f).BorderColor("#F0F0F0")
                         .PaddingVertical(6)
                         .PaddingHorizontal(8)
                         .Text(f.Total.ToString("C0", _ci)).Bold().FontColor(Verde).FontSize(9);

                    table.Cell().Background(bg).BorderBottom(0.5f).BorderColor("#F0F0F0")
                         .PaddingVertical(6)
                         .PaddingHorizontal(8)
                         .Text(f.TicketPromedio.ToString("C0", _ci)).FontColor(TextGris).FontSize(9);

                    table.Cell().Background(bg).BorderBottom(0.5f).BorderColor("#F0F0F0")
                         .PaddingVertical(4)
                         .PaddingHorizontal(6)
                         .Background("#F5F5F5").CornerRadius(10)
                         .PaddingHorizontal(8).PaddingVertical(2)
                         .Text(f.Empleado).FontSize(9).FontColor(TextGris);
                }

                var totalStr = _data.TotalIngresos.ToString("C0", _ci);
                table.Cell().ColumnSpan(3).Background(BgGris).BorderTop(1f).BorderColor(Verde)
                     .PaddingVertical(6)
                     .PaddingHorizontal(8)
                     .Text("TOTAL DE LA SEMANA").Bold().FontSize(9).FontColor(VerdeOsc);
                table.Cell().Background(BgGris).BorderTop(1f).BorderColor(Verde)
                     .PaddingVertical(6)
                     .PaddingHorizontal(8)
                     .Text(totalStr).Bold().FontColor(Verde).FontSize(10);
                table.Cell().ColumnSpan(2).Background(BgGris).BorderTop(1f).BorderColor(Verde)
                     .PaddingVertical(6)
                     .PaddingHorizontal(8)
                     .Text("");
            });
        }

        private static void TableHeader(TableDescriptor table, string[] headers)
        {
            table.Header(hdr =>
            {
                foreach (var h in headers)
                    hdr.Cell()
                        .Background("#2E7D32")
                        .PaddingVertical(6)
                        .PaddingHorizontal(8)
                       .Text(h).Bold().FontSize(9).FontColor("#FFFFFF");
            });
        }

        private void ComposeMetodosPago(IContainer c)
        {
            var total = _data.TotalIngresos == 0 ? 1 : _data.TotalIngresos;

            int pctEfectivo = (int)Math.Round(_data.TotalEfectivo / total * 100);
            int pctTarjeta = (int)Math.Round(_data.TotalTarjeta / total * 100);
            int pctTransferencia = (int)Math.Round(_data.TotalTransferencia / total * 100);

            c.Row(row =>
            {
                PagoCard(row.RelativeItem(), "Efectivo",
                    _data.TotalEfectivo.ToString("C0", _ci), $"{pctEfectivo}% del total");
                row.ConstantItem(12);
                PagoCard(row.RelativeItem(), "Tarjeta de crédito",
                    _data.TotalTarjeta.ToString("C0", _ci), $"{pctTarjeta}% del total");
                row.ConstantItem(12);
                PagoCard(row.RelativeItem(), "Transferencia",
                    _data.TotalTransferencia.ToString("C0", _ci), $"{pctTransferencia}% del total");
            });
        }

        private void PagoCard(IContainer c, string metodo, string monto, string pct)
        {
            c.Border(0.5f).BorderColor(BorderGris).CornerRadius(6).Padding(14).Column(col =>
            {
                col.Item().Text(metodo).FontSize(9).FontColor(TextGris);
                col.Item().Height(4);
                col.Item().Text(monto).FontSize(16).Bold().FontColor(VerdeOsc);
                col.Item().Height(2);
                col.Item().Text(pct).FontSize(8).FontColor("#AAAAAA");
            });
        }

        private void ComposeFooter(IContainer c)
        {
            c.BorderTop(0.5f).BorderColor(BorderGris).PaddingTop(8).Row(row =>
            {
                row.RelativeItem()
                   .Text("El Apóstol — Materiales de Construcción | " +
                         $"Reporte generado automáticamente el {_data.FechaGeneracion:dd/MM/yyyy}")
                   .FontSize(7).FontColor("#AAAAAA");

                row.AutoItem()
                   .Text("Documento confidencial — Solo para uso interno del administrador")
                   .FontSize(7).FontColor("#AAAAAA").AlignRight();
            });
        }
    }
}