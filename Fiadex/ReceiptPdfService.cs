using System.IO;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Fiadex;

/// <summary>
/// Genera el comprobante de saldo cero de un préstamo. Monocromático, en A4,
/// sin depender de impresoras ni de servicios externos.
/// </summary>
public static class ReceiptPdfService
{
    private static readonly Color Ink = Colors.Grey.Darken4;
    private static readonly Color Muted = Colors.Grey.Darken1;
    private static readonly Color Line = Colors.Grey.Lighten2;
    private static readonly Color Wash = Colors.Grey.Lighten4;

    static ReceiptPdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static byte[] Generate(ReceiptData receipt)
    {
        using var stream = new MemoryStream();

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(text => text.FontFamily("Lato").FontSize(10).FontColor(Ink));

                page.Header().Element(header => ComposeHeader(header, receipt));
                page.Content().PaddingVertical(20).Element(content => ComposeContent(content, receipt));
                page.Footer().Element(ComposeFooter);
            });
        }).GeneratePdf(stream);

        return stream.ToArray();
    }

    private static void ComposeHeader(IContainer container, ReceiptData receipt)
    {
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(brand =>
                {
                    brand.Item().Text("FIADEX").FontSize(20).SemiBold().FontColor(Ink);
                    brand.Item().PaddingTop(2).Text("Control de Préstamos y Cartera")
                        .FontSize(9).FontColor(Muted);
                });

                row.ConstantItem(190).AlignRight().Column(meta =>
                {
                    meta.Item().Text("COMPROBANTE DE PAGO").FontSize(11).SemiBold().FontColor(Ink);
                    meta.Item().PaddingTop(3).Text($"N.º {receipt.ReceiptNumber}").FontSize(9).FontColor(Muted);
                    meta.Item().Text($"Emitido: {receipt.IssuedAt:dd/MM/yyyy HH:mm}").FontSize(9).FontColor(Muted);
                });
            });

            column.Item().PaddingTop(14).BorderBottom(1).BorderColor(Line).Height(1);
        });
    }

    private static void ComposeContent(IContainer container, ReceiptData receipt)
    {
        container.Column(column =>
        {
            column.Spacing(18);

            column.Item().Row(row =>
            {
                row.RelativeItem().Element(c => ComposeInfoBlock(c, "CLIENTE",
                [
                    ("Nombre", receipt.ClientName),
                    ("Teléfono", string.IsNullOrWhiteSpace(receipt.ClientPhone) ? "—" : receipt.ClientPhone)
                ]));

                row.ConstantItem(16);

                row.RelativeItem().Element(c => ComposeInfoBlock(c, "PRÉSTAMO",
                [
                    ("Préstamo", $"{receipt.LoanId:000000}"),
                    ("Fecha", receipt.LoanDate.ToString("dd/MM/yyyy")),
                    ("Monto original", receipt.LoanAmount.ToString("C0"))
                ]));
            });

            column.Item().Text("DETALLE DE ABONOS").FontSize(10).SemiBold().FontColor(Ink);

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(4);
                });

                table.Header(header =>
                {
                    header.Cell().Element(CellHeader).Text("Fecha");
                    header.Cell().Element(CellHeader).Text("Monto");
                    header.Cell().Element(CellHeader).Text("Notas");
                });

                foreach (var payment in receipt.Payments)
                {
                    table.Cell().Element(CellBody).Text(payment.Date.ToString("dd/MM/yyyy"));
                    table.Cell().Element(CellBody).Text(payment.Amount.ToString("C0"));
                    table.Cell().Element(CellBody)
                        .Text(string.IsNullOrWhiteSpace(payment.Notes) ? "—" : payment.Notes);
                }
            });

            column.Item().Row(row =>
            {
                row.RelativeItem();

                row.ConstantItem(250).Column(summary =>
                {
                    summary.Item().PaddingTop(6).BorderTop(1).BorderColor(Line)
                        .Row(line =>
                        {
                            line.RelativeItem().Text("Total abonado").FontColor(Muted);
                            line.ConstantItem(110).AlignRight().Text(receipt.TotalPaid.ToString("C0")).SemiBold();
                        });

                    summary.Item().PaddingTop(5).Row(line =>
                    {
                        line.RelativeItem().Text("Saldo final").FontColor(Muted);
                        line.ConstantItem(110).AlignRight().Text(receipt.Balance.ToString("C0")).SemiBold();
                    });

                    summary.Item().PaddingTop(12).Background(Wash).Padding(9).AlignCenter()
                        .Text("PRÉSTAMO SALDADO").SemiBold().FontColor(Ink);
                });
            });

            if (!string.IsNullOrWhiteSpace(receipt.Notes))
            {
                column.Item().Column(notes =>
                {
                    notes.Item().Text("Observaciones del préstamo").FontSize(9).SemiBold().FontColor(Muted);
                    notes.Item().PaddingTop(3).Text(receipt.Notes).FontSize(9).FontColor(Muted);
                });
            }
        });
    }

    private static void ComposeInfoBlock(IContainer container, string title, IReadOnlyList<(string Label, string Value)> rows)
    {
        container.Column(column =>
        {
            column.Item().Text(title).FontSize(9).SemiBold().FontColor(Muted);
            column.Item().PaddingTop(5).PaddingLeft(9).BorderLeft(2).BorderColor(Colors.Grey.Darken2)
                .Column(items =>
                {
                    items.Spacing(4);
                    foreach (var (label, value) in rows)
                    {
                        items.Item().Text(text =>
                        {
                            text.Span($"{label}: ").FontColor(Muted);
                            text.Span(value).SemiBold();
                        });
                    }
                });
        });
    }

    private static IContainer CellHeader(IContainer container) =>
        container.Background(Wash).Padding(6).BorderBottom(1).BorderColor(Line);

    private static IContainer CellBody(IContainer container) =>
        container.Padding(6).BorderBottom(1).BorderColor(Colors.Grey.Lighten3);

    private static void ComposeFooter(IContainer container)
    {
        container.BorderTop(1).BorderColor(Line).PaddingTop(8).Row(row =>
        {
            row.RelativeItem().Text("Documento generado localmente por Fiadex")
                .FontSize(8).FontColor(Muted);
            row.ConstantItem(80).AlignRight().Text(text =>
            {
                text.DefaultTextStyle(style => style.FontSize(8).FontColor(Muted));
                text.Span("Página ");
                text.CurrentPageNumber();
            });
        });
    }
}
