using Microsoft.EntityFrameworkCore;
using PdfEditorApi.Models;

namespace PdfEditorApi.Data;

public static class SOPSeedData
{
    public const int AdminId = 1;
    public const int SupervisorId = 2;
    public const int Approver1Id = 3;
    public const int Approver2Id = 4;
    public const int Approver3Id = 5;
    public const int InitiatorId = 6;

    public static async Task EnsureSeededAsync(SOPDbContext db, CancellationToken cancellationToken = default)
    {
        await db.Database.EnsureCreatedAsync(cancellationToken);

        if (!await db.Users.AnyAsync(cancellationToken))
        {
            db.Users.AddRange(
                new User { Id = AdminId, UserName = "Siddhant_Admin", DisplayName = "Siddhant_Admin", Role = UserRole.Admin },
                new User { Id = SupervisorId, UserName = "Rahul_Supervisor", DisplayName = "Rahul_Supervisor", Role = UserRole.Supervisor },
                new User { Id = Approver1Id, UserName = "Pooja_Approver1", DisplayName = "Pooja_Approver1", Role = UserRole.Approver1 },
                new User { Id = Approver2Id, UserName = "Amit_Approver2", DisplayName = "Amit_Approver2", Role = UserRole.Approver2 },
                new User { Id = Approver3Id, UserName = "Priya_Approver3", DisplayName = "Priya_Approver3", Role = UserRole.Approver3 },
                new User { Id = InitiatorId, UserName = "SOP_Initiator", DisplayName = "SOP_Initiator", Role = UserRole.Initiator }
            );
        }

        if (!await db.SOPInstances.AnyAsync(cancellationToken))
        {
            var now = DateTime.UtcNow;
            var html =
                "<h2>Digital SOP</h2><p>Initial SOP content loaded from template.</p><table border='1' cellpadding='8'><tr><th>Step</th><th>Details</th></tr><tr><td>1</td><td>Fill checklist and submit.</td></tr></table>";
            var initiator = await db.Users.Where(u => u.Role == UserRole.Initiator).Select(u => u.Id).FirstOrDefaultAsync(cancellationToken);
            if (initiator == 0)
                initiator = AdminId;

            db.SOPInstances.Add(new SOPInstance
            {
                Id = Guid.NewGuid(),
                Title = "Digital SOP - Area 1",
                InitiatorId = initiator,
                CreatedByUserId = AdminId,
                AssignedToUserId = null,
                Status = SOPStatus.NotStarted,
                CurrentContentHtml = html,
                TemplateHtml = html,
                EditableSectionSelectorsJson = "[]",
                ExpiryDateUtc = now.AddYears(1),
                UploadedAtUtc = now,
                Area = "Area 1",
                Line = "Line 1",
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
