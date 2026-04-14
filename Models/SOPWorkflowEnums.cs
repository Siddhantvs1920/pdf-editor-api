namespace PdfEditorApi.Models;

public enum UserRole
{
    Admin = 1,
    Supervisor = 2,
    Approver1 = 3,
    Approver2 = 4,
    Approver3 = 5,
    Initiator = 6
}

public enum SOPStatus
{
    NotStarted,
    InProgress,
    Submitted,
    Pending_Supervisor,
    Pending_Appr1,
    Pending_Appr2,
    Pending_Appr3,
    Rejected,
    Completed,
    Expired
}
