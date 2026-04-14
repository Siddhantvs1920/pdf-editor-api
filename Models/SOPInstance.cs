namespace PdfEditorApi.Models;

public sealed class SOPInstance
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string CurrentContentHtml { get; set; } = "<p></p>";
    /// <summary>Baseline HTML for locked regions; updated when admin uploads a new document version.</summary>
    public string TemplateHtml { get; set; } = "<p></p>";
    /// <summary>JSON array of CSS selectors for editable regions, e.g. [\".sop-editable\"]. Empty = entire body editable.</summary>
    public string EditableSectionSelectorsJson { get; set; } = "[]";
    public SOPStatus Status { get; set; } = SOPStatus.NotStarted;
    /// <summary>Primary initiator / return-to user when rejected from approval chain.</summary>
    public int InitiatorId { get; set; }
    /// <summary>Admin user who uploaded the template.</summary>
    public int CreatedByUserId { get; set; }
    public int? AssignedToUserId { get; set; }
    public DateTime? ExpiryDateUtc { get; set; }
    public DateTime UploadedAtUtc { get; set; } = DateTime.UtcNow;
    public string? Area { get; set; }
    public string? Line { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public byte[]? RowVersion { get; set; }
}
