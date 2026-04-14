namespace PdfEditorApi.Models;

public sealed class ExportToPdfRequest
{
    public string Html { get; set; } = string.Empty;
    /// <summary>Optional safe file name without path (e.g. SOP-guid-Title-20260211.pdf).</summary>
    public string? DownloadFileName { get; set; }
}
