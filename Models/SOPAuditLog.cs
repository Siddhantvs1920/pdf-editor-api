namespace PdfEditorApi.Models;

public sealed class SOPAuditLog
{
    public Guid Id { get; set; }
    public Guid SOPInstanceId { get; set; }
    public int ActorUserId { get; set; }
    public string ActorRole { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Comments { get; set; } = string.Empty;
    public SOPStatus StatusAfterAction { get; set; }
    public int? AssignedToUserIdAfterAction { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}
