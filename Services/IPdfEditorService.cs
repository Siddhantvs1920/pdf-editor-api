namespace PdfEditorApi.Services;

public interface IPdfEditorService
{
    Task<byte[]> ExportHtmlToPdfAsync(string html, CancellationToken cancellationToken = default);
}
