namespace PdfEditorApi.Models;

public sealed class UpdateSOPStatusRequest
{
    public int CurrentUserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Comments { get; set; } = string.Empty;
}
