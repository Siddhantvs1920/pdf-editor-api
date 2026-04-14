namespace PdfEditorApi.Models;

public sealed class CreateSopRequest
{
    public int CurrentUserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Html { get; set; } = string.Empty;
    public DateTime? ExpiryDateUtc { get; set; }
    public string? Area { get; set; }
    public string? Line { get; set; }
    /// <summary>JSON array of CSS selectors, e.g. [\".sop-editable-section\"]. Empty or omit = full document editable for initiators.</summary>
    public string? EditableSectionSelectorsJson { get; set; }
}
