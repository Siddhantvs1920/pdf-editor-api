namespace PdfEditorApi.Models;

public sealed class AdminUpdateSopRequest
{
    public int CurrentUserId { get; set; }
    public DateTime? ExpiryDateUtc { get; set; }
    public string? Area { get; set; }
    public string? Line { get; set; }
    public string? EditableSectionSelectorsJson { get; set; }
}
