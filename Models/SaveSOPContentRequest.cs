namespace PdfEditorApi.Models;

public sealed class SaveSOPContentRequest
{
    public int CurrentUserId { get; set; }
    public string Html { get; set; } = string.Empty;
}
