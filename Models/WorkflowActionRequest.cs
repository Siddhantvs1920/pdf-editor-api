namespace PdfEditorApi.Models;

public sealed class WorkflowActionRequest
{
    public int CurrentUserId { get; set; }
    /// <summary>approve | reject | request_changes | submit_for_approval (initiator)</summary>
    public string Action { get; set; } = string.Empty;
    public string Comments { get; set; } = string.Empty;
    public string? Signature { get; set; }
}
