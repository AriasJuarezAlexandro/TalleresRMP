using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TalleresRMP.Models;

namespace TalleresRMP.Services;

/// <summary>
/// Genera el PDF de la proforma replicando el diseño del documento físico
/// del taller usando QuestPDF.
/// </summary>
public class ProformaPdfService
{
    private static readonly string VerdeOscuro = "#1a5c2a";

    private readonly string _logoPath;
    private readonly string _bcpPath;
    private readonly string _yapePath;

    public ProformaPdfService(IWebHostEnvironment env)
    {
        var imagenes = Path.Combine(env.WebRootPath ?? "wwwroot", "images");
        _logoPath = Path.Combine(imagenes, "logo.png");
        _bcpPath = Path.Combine(imagenes, "bcp.png");
        _yapePath = Path.Combine(imagenes, "yape.png");
    }

    public byte[] GenerarProforma(Mantenimiento m)
    {
        var documento = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(10).FontColor(Colors.Black));

                page.Header().Element(c => ComponerEncabezado(c, m));
                page.Content().Element(c => ComponerContenido(c, m));
                page.Footer().Element(ComponerPie);
            });
        });

        return documento.GeneratePdf();
    }

    private void ComponerEncabezado(IContainer container, Mantenimiento m)
    {
        container.PaddingBottom(10).Column(col =>
        {
            col.Item().Row(row =>
            {
                // Logo a la izquierda
                row.ConstantItem(90).Height(70).AlignMiddle().Element(c =>
                {
                    if (File.Exists(_logoPath))
                        c.Image(_logoPath).FitArea();
                    else
                        c.AlignCenter().AlignMiddle().Text("RMP").Bold().FontSize(20).FontColor(VerdeOscuro);
                });

                // Datos del taller al centro
                row.RelativeItem().AlignMiddle().Column(centro =>
                {
                    centro.Item().AlignCenter().Text("TALLER MECANICA GENERAL")
                        .Bold().FontSize(15).FontColor(VerdeOscuro);
                    centro.Item().AlignCenter().Text("RMP — Servicio Automotriz");
                    centro.Item().AlignCenter().Text(
                        "MECANICA EN GENERAL, SUSPENSIÓN, FRENOS, LIMPIEZA DE INYECTORES, " +
                        "ELECTRICIDAD, SERVICIO DE AFINAMIENTO Y MANTENIMIENTO")
                        .FontSize(7).FontColor(Colors.Grey.Darken2);
                    centro.Item().AlignCenter().Text("RUC: 10452787054").FontSize(9);
                    centro.Item().AlignCenter().Text("EMAIL: TALLERESRMPDIESEL1@GMAIL.COM").FontSize(9);
                });

                // Recuadro proforma a la derecha
                row.ConstantItem(140).Border(1).BorderColor(VerdeOscuro).Padding(6).Column(der =>
                {
                    der.Item().AlignCenter().Text("PROFORMA").Bold().FontColor(VerdeOscuro);
                    der.Item().AlignCenter().Text($"N° {m.Numero}").Bold().FontSize(13);
                    der.Item().AlignCenter().Text(m.FechaCreacion.ToString("dd/MM/yyyy"));
                });
            });
        });
    }

    private void ComponerContenido(IContainer container, Mantenimiento m)
    {
        container.Column(col =>
        {
            // Datos del vehículo / cliente
            col.Item().PaddingVertical(8).Border(1).BorderColor(Colors.Grey.Lighten1).Padding(8).Column(datos =>
            {
                datos.Item().Row(r =>
                {
                    r.RelativeItem().Text(t => { t.Span("Cliente: ").Bold(); t.Span(m.Cliente); });
                    r.RelativeItem().Text(t => { t.Span("Teléfono: ").Bold(); t.Span(m.Telefono); });
                });
                datos.Item().Row(r =>
                {
                    r.RelativeItem().Text(t => { t.Span("Marca / Modelo: ").Bold(); t.Span($"{m.Marca} {m.Modelo}"); });
                    r.RelativeItem().Text(t => { t.Span("Placa: ").Bold(); t.Span(m.Placa); });
                });
                datos.Item().Row(r =>
                {
                    r.RelativeItem().Text(t => { t.Span("KM: ").Bold(); t.Span(m.KM); });
                    r.RelativeItem().Text(t => { t.Span("Fecha: ").Bold(); t.Span(m.FechaCreacion.ToString("dd/MM/yyyy")); });
                });
            });

            // Tabla de productos
            col.Item().PaddingTop(10).Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(40);   // Item
                    c.ConstantColumn(45);   // Cant.
                    c.RelativeColumn();      // Descripción
                    c.ConstantColumn(70);   // P. Unit
                    c.ConstantColumn(75);   // Importe
                });

                table.Header(header =>
                {
                    HeaderCell(header, "Item");
                    HeaderCell(header, "Cant.");
                    HeaderCell(header, "Descripción");
                    HeaderCell(header, "P. Unit");
                    HeaderCell(header, "Importe");
                });

                foreach (var p in m.Productos)
                {
                    BodyCell(table, p.Item, TextAlignment.Center);
                    BodyCell(table, p.Cantidad.ToString(), TextAlignment.Center);
                    BodyCell(table, p.Descripcion, TextAlignment.Left);
                    BodyCell(table, p.PrecioUnitario.ToString("N2"), TextAlignment.Right);
                    BodyCell(table, p.Importe.ToString("N2"), TextAlignment.Right);
                }
            });

            // Total al pie derecho
            col.Item().PaddingTop(8).AlignRight().Row(row =>
            {
                row.ConstantItem(220).Border(1).BorderColor(VerdeOscuro).Background(Colors.Grey.Lighten4).Padding(6).Row(r =>
                {
                    r.RelativeItem().Text("TOTAL").Bold().FontColor(VerdeOscuro);
                    r.ConstantItem(90).AlignRight().Text($"S/ {m.Total:N2}").Bold().FontSize(13);
                });
            });
        });
    }

    private void ComponerPie(IContainer container)
    {
        container.BorderTop(1).BorderColor(Colors.Grey.Lighten1).PaddingTop(6).Column(col =>
        {
            col.Item().AlignCenter().Text("Datos para depósito / transferencia").Bold().FontColor(VerdeOscuro);

            col.Item().PaddingTop(4).Row(row =>
            {
                // Columna BCP: imagen + CC / CCI
                row.RelativeItem().Column(bcp =>
                {
                    bcp.Item().Height(45).AlignCenter().Element(c =>
                    {
                        if (File.Exists(_bcpPath))
                            c.Image(_bcpPath).FitHeight();
                        else
                            c.AlignMiddle().Text("BCP").Bold().FontColor(Colors.Orange.Darken2);
                    });
                    bcp.Item().AlignCenter().Text("CC: 19497432787054").FontSize(9);
                    bcp.Item().AlignCenter().Text("CCI: 00219419743278705494").FontSize(9);
                });

                // Columna Yape: imagen + nombre / celular
                row.RelativeItem().Column(yape =>
                {
                    yape.Item().Height(45).AlignCenter().Element(c =>
                    {
                        if (File.Exists(_yapePath))
                            c.Image(_yapePath).FitHeight();
                        else
                            c.AlignMiddle().Text("YAPE").Bold().FontColor(Colors.Purple.Darken2);
                    });
                    yape.Item().AlignCenter().Text("MARGARITA SULCA").FontSize(9);
                    yape.Item().AlignCenter().Text("CEL: 952708965").FontSize(9);
                });
            });
        });
    }

    private static void HeaderCell(TableCellDescriptor header, string texto)
    {
        header.Cell().Border(1).BorderColor(VerdeOscuro).Background(VerdeOscuro).Padding(4)
            .Text(texto).Bold().FontColor(Colors.White);
    }

    private static void BodyCell(TableDescriptor table, string texto, TextAlignment alignment)
    {
        IContainer cell = table.Cell().Border(1).BorderColor(Colors.Grey.Medium).Padding(4);
        cell = alignment switch
        {
            TextAlignment.Center => cell.AlignCenter(),
            TextAlignment.Right => cell.AlignRight(),
            _ => cell
        };
        cell.Text(texto);
    }

    private enum TextAlignment { Left, Center, Right }
}
