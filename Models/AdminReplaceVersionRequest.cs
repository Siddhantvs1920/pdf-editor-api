namespace PdfEditorApi.Models;

public sealed class AdminReplaceVersionRequest
{
    public int CurrentUserId { get; set; }
    public string Html { get; set; } = string.Empty;
}
