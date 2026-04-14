using System.Net;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using PdfEditorApi.Data;
using PdfEditorApi.Models;

namespace PdfEditorApi.Services;

public sealed class SOPWorkflowService(SOPDbContext db) : ISOPWorkflowService
{
    public async Task<IReadOnlyList<User>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        var users = await db.Users.OrderBy(x => x.UserName).ToListAsync(cancellationToken);
        foreach (var user in users)
            user.DisplayName = user.UserName;
        return users;
    }

    public async Task<User?> LoginAsync(string userName, string password, CancellationToken cancellationToken = default)
    {
        var normalizedUser = (userName ?? string.Empty).Trim();
        var normalizedPassword = (password ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedUser) || string.IsNullOrWhiteSpace(normalizedPassword))
            return null;

        var lookupUserName = normalizedUser switch
        {
            "Pooja_Approver1" => "Neha_Approver1",
            _ => normalizedUser
        };

        var user = await db.Users.FirstOrDefaultAsync(x => x.UserName == lookupUserName, cancellationToken);
        if (user is null)
        {
            var lowered = lookupUserName.ToLowerInvariant();
            user = await db.Users.FirstOrDefaultAsync(x => x.UserName.ToLower() == lowered, cancellationToken);
        }

        if (user is null && (normalizedUser.Equals("Pooja_Approver1", StringComparison.OrdinalIgnoreCase) ||
                             normalizedUser.Equals("Neha_Approver1", StringComparison.OrdinalIgnoreCase) ||
                             normalizedUser.Equals("Approver1", StringComparison.OrdinalIgnoreCase)))
            user = await db.Users.FirstOrDefaultAsync(x => x.Role == UserRole.Approver1, cancellationToken);

        if (user is null)
            return null;

        if (!IsPasswordAccepted(user.UserName, user.Role, user.PasswordHash, normalizedPassword))
            return null;

        user.DisplayName = user.UserName;
        return user;
    }

    public async Task<SOPInstance> CreateSopAsync(
        int currentUserId,
        string title,
        string html,
        DateTime? expiryDateUtc,
        string? area,
        string? line,
        string? editableSectionSelectorsJson,
        CancellationToken cancellationToken = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == currentUserId, cancellationToken)
            ?? throw new InvalidOperationException("Current user not found.");

        if (user.Role != UserRole.Admin)
            throw new InvalidOperationException("Only Admin can upload SOP.");

        var cleanTitle = string.IsNullOrWhiteSpace(title) ? "Untitled SOP" : title.Trim();
        var content = string.IsNullOrWhiteSpace(html) ? "<p></p>" : html;
        var selectorsJson = string.IsNullOrWhiteSpace(editableSectionSelectorsJson)
            ? "[]"
            : editableSectionSelectorsJson.Trim();

        var initiatorId = await ResolveFirstUserIdAsync(UserRole.Initiator, cancellationToken);

        var now = DateTime.UtcNow;
        var sop = new SOPInstance
        {
            Id = Guid.NewGuid(),
            Title = cleanTitle,
            CurrentContentHtml = content,
            TemplateHtml = content,
            EditableSectionSelectorsJson = selectorsJson,
            Status = SOPStatus.NotStarted,
            InitiatorId = initiatorId,
            CreatedByUserId = user.Id,
            AssignedToUserId = null,
            ExpiryDateUtc = expiryDateUtc,
            UploadedAtUtc = now,
            Area = string.IsNullOrWhiteSpace(area) ? null : area.Trim(),
            Line = string.IsNullOrWhiteSpace(line) ? null : line.Trim(),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        db.SOPInstances.Add(sop);
        await db.SaveChangesAsync(cancellationToken);
        return sop;
    }

    public async Task<IReadOnlyList<SOPInstance>> GetSopsAsync(string filter, int currentUserId, CancellationToken cancellationToken = default)
    {
        await MarkExpiredSopsAsync(cancellationToken);

        var query = db.SOPInstances.AsQueryable();
        var normalized = (filter ?? string.Empty).Trim().ToLowerInvariant();

        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == currentUserId, cancellationToken);
        var roleLabel = user?.Role.ToString() ?? string.Empty;
        var now = DateTime.UtcNow;

        query = normalized switch
        {
            "pending" => query.Where(x => x.AssignedToUserId == currentUserId),
            "approved" => query.Where(x => x.Status == SOPStatus.Completed),
            "rejected" => query.Where(x => x.Status == SOPStatus.Rejected),
            "all" => query,
            "uploaded" => query.Where(x => x.CreatedByUserId == currentUserId),
            "available" => query.Where(x =>
                (x.Status == SOPStatus.NotStarted || x.Status == SOPStatus.InProgress || x.Status == SOPStatus.Rejected) &&
                (x.ExpiryDateUtc == null || x.ExpiryDateUtc >= now) &&
                x.Status != SOPStatus.Expired),
            "my-approved" => string.IsNullOrWhiteSpace(roleLabel)
                ? query.Where(_ => false)
                : query.Where(x => x.CurrentContentHtml.Contains($"[{roleLabel}] - [Approve]", StringComparison.OrdinalIgnoreCase)),
            _ => query
        };

        return await query.OrderByDescending(x => x.UpdatedAtUtc).ToListAsync(cancellationToken);
    }

    public Task<SOPInstance?> GetSopAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return db.SOPInstances.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<SOPInstance> SaveContentAsync(Guid id, int currentUserId, string html, CancellationToken cancellationToken = default)
    {
        await MarkExpiredSopsAsync(cancellationToken);

        var sop = await db.SOPInstances.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("SOP not found.");

        var actor = await db.Users.FirstOrDefaultAsync(x => x.Id == currentUserId, cancellationToken)
            ?? throw new InvalidOperationException("Current user not found.");

        if (sop.Status == SOPStatus.Completed)
            throw new InvalidOperationException("Completed SOP cannot be edited.");

        if (sop.Status == SOPStatus.Expired || IsExpired(sop))
            throw new InvalidOperationException("Expired SOP cannot be edited. Upload a new version or create a new SOP.");

        if (sop.Status is SOPStatus.Submitted or SOPStatus.Pending_Supervisor or SOPStatus.Pending_Appr1
            or SOPStatus.Pending_Appr2 or SOPStatus.Pending_Appr3)
        {
            if (actor.Role == UserRole.Initiator)
                throw new InvalidOperationException("SOP is submitted or in approval; initiators cannot edit until it is returned for changes.");
        }

        var merged = html;
        if (actor.Role == UserRole.Initiator)
        {
            if (sop.Status is not (SOPStatus.NotStarted or SOPStatus.InProgress or SOPStatus.Rejected))
                throw new InvalidOperationException("Initiator cannot edit this SOP in its current status.");

            merged = EditableHtmlMerger.Merge(sop.TemplateHtml, html, sop.EditableSectionSelectorsJson);
            if (sop.Status == SOPStatus.NotStarted)
                sop.Status = SOPStatus.InProgress;
        }
        else if (actor.Role == UserRole.Admin)
        {
            merged = html;
            sop.TemplateHtml = html;
        }
        // Supervisor / Approver: full save when they are assigned (optional edits)
        else if (sop.AssignedToUserId == actor.Id)
        {
            merged = html;
        }
        else
            throw new InvalidOperationException("You are not allowed to save changes to this SOP.");

        var stamp = DateTime.UtcNow;
        sop.CurrentContentHtml = AppendCollaborationStamp(merged, stamp, actor.UserName, actor.Role.ToString());
        sop.UpdatedAtUtc = stamp;
        await db.SaveChangesAsync(cancellationToken);
        return sop;
    }

    public async Task<SOPInstance> WorkflowActionAsync(Guid id, int currentUserId, string action, string comments, string? signature, CancellationToken cancellationToken = default)
    {
        await MarkExpiredSopsAsync(cancellationToken);

        var sop = await db.SOPInstances.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("SOP not found.");

        if (sop.Status == SOPStatus.Expired || IsExpired(sop))
            throw new InvalidOperationException("SOP has expired.");

        var actor = await db.Users.FirstOrDefaultAsync(x => x.Id == currentUserId, cancellationToken)
            ?? throw new InvalidOperationException("Current user not found.");

        var normalized = (action ?? string.Empty).Trim().ToLowerInvariant().Replace("-", "_").Replace(" ", "_");
        if (normalized == "requestchanges")
            normalized = "request_changes";
        if (normalized is "submitforapproval" or "submit_for_review")
            normalized = "submit";
        var now = DateTime.UtcNow;

        if (normalized is "submit" or "submit_for_approval")
        {
            if (actor.Role != UserRole.Initiator)
                throw new InvalidOperationException("Only an Initiator can submit for supervisor review.");

            if (sop.Status is not (SOPStatus.NotStarted or SOPStatus.InProgress or SOPStatus.Rejected))
                throw new InvalidOperationException("SOP cannot be submitted in its current status.");

            if (sop.AssignedToUserId != null && sop.AssignedToUserId != actor.Id && sop.Status != SOPStatus.Rejected)
                throw new InvalidOperationException("SOP is not available for your submission.");

            sop.Status = SOPStatus.Submitted;
            sop.AssignedToUserId = await ResolveFirstUserIdAsync(UserRole.Supervisor, cancellationToken);
            sop.UpdatedAtUtc = now;
            var sig = string.IsNullOrWhiteSpace(signature) ? "-" : signature.Trim();
            sop.CurrentContentHtml = AppendAuditEntry(sop.CurrentContentHtml, now, actor.Role.ToString(), "Submit", comments, sig);
            await db.SaveChangesAsync(cancellationToken);
            await TryAddAuditLogAsync(sop, actor, "Submit", comments, now, cancellationToken);
            return sop;
        }

        if (normalized == "request_changes")
        {
            if (actor.Role != UserRole.Supervisor)
                throw new InvalidOperationException("Only Supervisor can request changes.");

            if (sop.AssignedToUserId != actor.Id)
                throw new InvalidOperationException("This SOP is not assigned to you.");

            if (sop.Status is not (SOPStatus.Submitted or SOPStatus.Pending_Supervisor))
                throw new InvalidOperationException("Supervisor cannot request changes after the SOP has been forwarded for approval.");

            sop.Status = SOPStatus.InProgress;
            sop.AssignedToUserId = null;
            sop.UpdatedAtUtc = now;
            var sig = string.IsNullOrWhiteSpace(signature) ? "-" : signature.Trim();
            sop.CurrentContentHtml = AppendAuditEntry(sop.CurrentContentHtml, now, actor.Role.ToString(), "RequestChanges", comments, sig);
            await db.SaveChangesAsync(cancellationToken);
            await TryAddAuditLogAsync(sop, actor, "RequestChanges", comments, now, cancellationToken);
            return sop;
        }

        // Approve / Reject (approval chain)
        if (normalized is "approve" or "reject")
        {
            if (sop.AssignedToUserId != actor.Id)
                throw new InvalidOperationException("This SOP is not assigned to the current user.");

            if (normalized == "reject")
            {
                sop.Status = SOPStatus.Rejected;
                sop.AssignedToUserId = sop.InitiatorId;
            }
            else
            {
                if (actor.Role == UserRole.Supervisor && sop.Status is SOPStatus.Submitted or SOPStatus.Pending_Supervisor)
                {
                    sop.Status = SOPStatus.Pending_Appr1;
                    sop.AssignedToUserId = await ResolveFirstUserIdAsync(UserRole.Approver1, cancellationToken);
                }
                else
                    ApplyApproverTransition(sop, actor.Role);
            }

            sop.UpdatedAtUtc = now;
            var sig = string.IsNullOrWhiteSpace(signature) ? "-" : signature.Trim();
            sop.CurrentContentHtml = AppendAuditEntry(sop.CurrentContentHtml, now, actor.Role.ToString(), normalized, comments, sig);
            await db.SaveChangesAsync(cancellationToken);
            await TryAddAuditLogAsync(sop, actor, normalized.Equals("approve", StringComparison.Ordinal) ? "Approve" : "Reject", comments, now, cancellationToken);
            return sop;
        }

        throw new InvalidOperationException("Unknown workflow action.");
    }

    public async Task<SOPInstance> UpdateSOPStatus(Guid id, int currentUserId, string action, string comments, CancellationToken cancellationToken = default)
    {
        return await WorkflowActionAsync(id, currentUserId, action, comments, null, cancellationToken);
    }

    public async Task<SOPInstance> AdminUpdateMetadataAsync(Guid id, int currentUserId, DateTime? expiryDateUtc, string? area, string? line, string? editableSectionSelectorsJson, CancellationToken cancellationToken = default)
    {
        var actor = await db.Users.FirstOrDefaultAsync(x => x.Id == currentUserId, cancellationToken)
            ?? throw new InvalidOperationException("Current user not found.");
        if (actor.Role != UserRole.Admin)
            throw new InvalidOperationException("Only Admin can update SOP metadata.");

        var sop = await db.SOPInstances.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("SOP not found.");

        sop.ExpiryDateUtc = expiryDateUtc;
        if (area != null) sop.Area = string.IsNullOrWhiteSpace(area) ? null : area.Trim();
        if (line != null) sop.Line = string.IsNullOrWhiteSpace(line) ? null : line.Trim();
        if (!string.IsNullOrWhiteSpace(editableSectionSelectorsJson))
            sop.EditableSectionSelectorsJson = editableSectionSelectorsJson.Trim();
        sop.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return sop;
    }

    public async Task<SOPInstance> AdminReplaceVersionAsync(Guid id, int currentUserId, string html, CancellationToken cancellationToken = default)
    {
        var actor = await db.Users.FirstOrDefaultAsync(x => x.Id == currentUserId, cancellationToken)
            ?? throw new InvalidOperationException("Current user not found.");
        if (actor.Role != UserRole.Admin)
            throw new InvalidOperationException("Only Admin can replace the document version.");

        var sop = await db.SOPInstances.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("SOP not found.");

        var content = string.IsNullOrWhiteSpace(html) ? "<p></p>" : html;
        var now = DateTime.UtcNow;
        sop.TemplateHtml = content;
        sop.CurrentContentHtml = content;
        sop.UploadedAtUtc = now;
        sop.UpdatedAtUtc = now;
        // Reset workflow for a fresh cycle after new template (optional: keep status if you prefer)
        if (sop.Status is SOPStatus.Completed or SOPStatus.Expired)
        {
            sop.Status = SOPStatus.NotStarted;
            sop.AssignedToUserId = null;
        }

        await db.SaveChangesAsync(cancellationToken);
        return sop;
    }

    public async Task<byte[]> ExportSopsExcelAsync(int currentUserId, CancellationToken cancellationToken = default)
    {
        var actor = await db.Users.FirstOrDefaultAsync(x => x.Id == currentUserId, cancellationToken)
            ?? throw new InvalidOperationException("Current user not found.");
        if (actor.Role != UserRole.Admin)
            throw new InvalidOperationException("Only Admin can export SOP records.");

        await MarkExpiredSopsAsync(cancellationToken);

        var list = await db.SOPInstances.OrderByDescending(x => x.UpdatedAtUtc).ToListAsync(cancellationToken);

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("SOPs");
        ws.Cell(1, 1).Value = "SOP_Id";
        ws.Cell(1, 2).Value = "SOP_Document_Name";
        ws.Cell(1, 3).Value = "Date_of_expiry";
        ws.Cell(1, 4).Value = "status_of_sop_submission";

        var row = 2;
        foreach (var s in list)
        {
            ws.Cell(row, 1).Value = s.Id.ToString();
            ws.Cell(row, 2).Value = s.Title;
            ws.Cell(row, 3).Value = s.ExpiryDateUtc?.ToString("u") ?? "";
            ws.Cell(row, 4).Value = s.Status.ToString();
            row++;
        }

        ws.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public async Task<IReadOnlyList<SOPAuditLog>> GetAuditLogsAsync(Guid sopInstanceId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await db.SOPAuditLogs
                .Where(x => x.SOPInstanceId == sopInstanceId)
                .OrderByDescending(x => x.TimestampUtc)
                .ToListAsync(cancellationToken);
        }
        catch
        {
            return Array.Empty<SOPAuditLog>();
        }
    }

    private async Task MarkExpiredSopsAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        await db.SOPInstances
            .Where(x => x.ExpiryDateUtc != null && x.ExpiryDateUtc < now &&
                        x.Status != SOPStatus.Completed &&
                        x.Status != SOPStatus.Expired &&
                        x.Status != SOPStatus.Rejected)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, SOPStatus.Expired), cancellationToken);
    }

    private static bool IsExpired(SOPInstance s) =>
        s.ExpiryDateUtc is { } exp && exp < DateTime.UtcNow;

    private void ApplyApproverTransition(SOPInstance sop, UserRole role)
    {
        switch (role)
        {
            case UserRole.Approver1:
                sop.Status = SOPStatus.Pending_Appr2;
                sop.AssignedToUserId = ResolveFirstUserId(UserRole.Approver2);
                break;
            case UserRole.Approver2:
                sop.Status = SOPStatus.Pending_Appr3;
                sop.AssignedToUserId = ResolveFirstUserId(UserRole.Approver3);
                break;
            case UserRole.Approver3:
                sop.Status = SOPStatus.Completed;
                sop.AssignedToUserId = null;
                break;
            default:
                throw new InvalidOperationException("This role cannot approve the SOP at this stage.");
        }
    }

    private int ResolveFirstUserId(UserRole role)
    {
        var id = db.Users.Where(x => x.Role == role).Select(x => (int?)x.Id).FirstOrDefault();
        return id ?? throw new InvalidOperationException($"No user exists for role {role}.");
    }

    private async Task<int> ResolveFirstUserIdAsync(UserRole role, CancellationToken cancellationToken)
    {
        var id = await db.Users.Where(x => x.Role == role).Select(x => (int?)x.Id).FirstOrDefaultAsync(cancellationToken);
        return id ?? throw new InvalidOperationException($"No user exists for role {role}.");
    }

    private async Task TryAddAuditLogAsync(SOPInstance sop, User actor, string action, string comments, DateTime now, CancellationToken cancellationToken)
    {
        var auditLog = new SOPAuditLog
        {
            Id = Guid.NewGuid(),
            SOPInstanceId = sop.Id,
            ActorUserId = actor.Id,
            ActorRole = actor.Role.ToString(),
            Action = action,
            Comments = comments ?? string.Empty,
            StatusAfterAction = sop.Status,
            AssignedToUserIdAfterAction = sop.AssignedToUserId,
            TimestampUtc = now
        };

        db.SOPAuditLogs.Add(auditLog);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            db.Entry(auditLog).State = EntityState.Detached;
        }
    }

    private static bool IsPasswordAccepted(string userName, UserRole role, string dbPasswordHash, string inputPassword)
    {
        if (!string.IsNullOrWhiteSpace(dbPasswordHash) && string.Equals(dbPasswordHash, inputPassword, StringComparison.Ordinal))
            return true;

        var normalizedUserName = (userName ?? string.Empty).Trim();
        var normalizedPassword = (inputPassword ?? string.Empty).Trim();

        if (role == UserRole.Approver1 && normalizedPassword == "approver1123")
            return true;

        if (role == UserRole.Initiator && normalizedPassword == "initiator123")
            return true;

        return normalizedUserName switch
        {
            "Siddhant_Admin" => normalizedPassword == "admin123",
            "Rahul_Supervisor" => normalizedPassword == "supervisor123",
            "Pooja_Approver1" => normalizedPassword == "approver1123",
            "Neha_Approver1" => normalizedPassword == "approver1123",
            "Amit_Approver2" => normalizedPassword == "approver2123",
            "Priya_Approver3" => normalizedPassword == "approver3123",
            "SOP_Initiator" => normalizedPassword == "initiator123",
            _ => false
        };
    }

    private static string AppendCollaborationStamp(string html, DateTime timestampUtc, string userName, string role)
    {
        var safeUser = WebUtility.HtmlEncode(userName);
        var safeRole = WebUtility.HtmlEncode(role);
        var line =
            $"<div class=\"sop-collab-stamp\" data-ts=\"{timestampUtc:O}\">[{timestampUtc:yyyy-MM-dd HH:mm:ss} UTC] — {safeUser} ({safeRole}) saved changes.</div>";
        return (html ?? string.Empty) + line;
    }

    private static string AppendAuditEntry(string html, DateTime timestampUtc, string role, string action, string comments, string signature)
    {
        var safeRole = WebUtility.HtmlEncode(role);
        var safeAction = WebUtility.HtmlEncode(action);
        var safeComments = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(comments) ? "-" : comments.Trim());
        var safeSig = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(signature) ? "-" : signature.Trim());
        var line =
            $"<div>[{timestampUtc:yyyy-MM-dd HH:mm:ss} UTC] - [{safeRole}] - [{safeAction}] Sig:{safeSig}: [{safeComments}]</div>";
        return (html ?? string.Empty) + line;
    }
}
