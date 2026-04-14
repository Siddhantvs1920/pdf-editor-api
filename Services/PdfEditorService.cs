using Aspose.Words;

namespace PdfEditorApi.Services;

public sealed class PdfEditorService : IPdfEditorService
{
    public Task<byte[]> ExportHtmlToPdfAsync(string html, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            var document = html.Trim();
            if (!document.Contains("<html", StringComparison.OrdinalIgnoreCase))
            {
                document =
                    "<!DOCTYPE html><html><head><meta charset=\"utf-8\" />" +
                    "<style>" +
                    "table{border-collapse:collapse;width:100%;}" +
                    "table,th,td{border:1px solid #111;}" +
                    "th,td{padding:6px;vertical-align:top;}" +
                    "</style></head><body>" +
                    document +
                    "</body></html>";
            }
            else if (!document.Contains("<style", StringComparison.OrdinalIgnoreCase))
            {
                // Ensure tables keep borders in the exported PDF even if editor stripped inline borders.
                document = document.Replace(
                    "</head>",
                    "<style>table{border-collapse:collapse;width:100%;}table,th,td{border:1px solid #111;}th,td{padding:6px;vertical-align:top;}</style></head>",
                    StringComparison.OrdinalIgnoreCase);
            }
            using var htmlStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(document));
            var loadOptions = new Aspose.Words.Loading.LoadOptions(Aspose.Words.LoadFormat.Html, string.Empty, null);
            var wordsDoc = new Document(htmlStream, loadOptions);
            using var output = new MemoryStream();
            wordsDoc.Save(output, Aspose.Words.SaveFormat.Pdf);
            return output.ToArray();
        }, cancellationToken);
    }
}
