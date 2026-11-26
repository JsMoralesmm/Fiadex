using System.IO;
using Microsoft.Win32;

namespace Fiadex;

public interface IReceiptExportService
{
    /// <returns>True si el usuario eligió una ubicación y el archivo se guardó.</returns>
    bool Export(ReceiptData receipt);
}

public sealed class ReceiptExportService : IReceiptExportService
{
    public bool Export(ReceiptData receipt)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Guardar comprobante de pago",
            Filter = "Archivos PDF (*.pdf)|*.pdf",
            DefaultExt = ".pdf",
            AddExtension = true,
            FileName = BuildFileName(receipt)
        };

        if (dialog.ShowDialog() != true)
        {
            return false;
        }

        File.WriteAllBytes(dialog.FileName, ReceiptPdfService.Generate(receipt));
        return true;
    }

    private static string BuildFileName(ReceiptData receipt)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var client = new string(receipt.ClientName
            .Select(character => invalid.Contains(character) ? '_' : character)
            .ToArray())
            .Trim()
            .Replace(' ', '_');

        if (string.IsNullOrWhiteSpace(client))
        {
            client = "Cliente";
        }

        if (client.Length > 40)
        {
            client = client[..40];
        }

        return $"Recibo_{client}_{receipt.IssuedAt:yyyyMMdd}.pdf";
    }
}
