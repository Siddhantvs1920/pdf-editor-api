using Microsoft.EntityFrameworkCore;

namespace PdfEditorApi.Data;

public static class DatabaseSchemaBootstrap
{
    /// <summary>
    /// SQL Server validates an entire batch before execution; ADD COLUMN + UPDATE in one batch can fail with
    /// "Invalid column name" for the new column. Run ALTER and UPDATE as separate commands.
    /// </summary>
    public static async Task EnsureSopInstanceColumnsAsync(SOPDbContext db, CancellationToken cancellationToken = default)
    {
        await db.Database.ExecuteSqlRawAsync(
            """
            IF COL_LENGTH('dbo.SOPInstances','TemplateHtml') IS NULL
              ALTER TABLE dbo.SOPInstances ADD TemplateHtml NVARCHAR(MAX) NULL;
            """, cancellationToken);
        await db.Database.ExecuteSqlRawAsync(
            """
            IF COL_LENGTH('dbo.SOPInstances','TemplateHtml') IS NOT NULL
              UPDATE dbo.SOPInstances SET TemplateHtml = CurrentContentHtml WHERE TemplateHtml IS NULL;
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync(
            """
            IF COL_LENGTH('dbo.SOPInstances','EditableSectionSelectorsJson') IS NULL
              ALTER TABLE dbo.SOPInstances ADD EditableSectionSelectorsJson NVARCHAR(MAX) NULL;
            """, cancellationToken);
        await db.Database.ExecuteSqlRawAsync(
            """
            IF COL_LENGTH('dbo.SOPInstances','EditableSectionSelectorsJson') IS NOT NULL
              UPDATE dbo.SOPInstances SET EditableSectionSelectorsJson = N'[]' WHERE EditableSectionSelectorsJson IS NULL;
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync(
            """
            IF COL_LENGTH('dbo.SOPInstances','CreatedByUserId') IS NULL
              ALTER TABLE dbo.SOPInstances ADD CreatedByUserId INT NULL;
            """, cancellationToken);
        await db.Database.ExecuteSqlRawAsync(
            """
            IF COL_LENGTH('dbo.SOPInstances','CreatedByUserId') IS NOT NULL
              UPDATE dbo.SOPInstances SET CreatedByUserId = InitiatorId WHERE CreatedByUserId IS NULL;
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync(
            """
            IF COL_LENGTH('dbo.SOPInstances','ExpiryDateUtc') IS NULL
              ALTER TABLE dbo.SOPInstances ADD ExpiryDateUtc DATETIME2 NULL;
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync(
            """
            IF COL_LENGTH('dbo.SOPInstances','UploadedAtUtc') IS NULL
              ALTER TABLE dbo.SOPInstances ADD UploadedAtUtc DATETIME2 NULL;
            """, cancellationToken);
        await db.Database.ExecuteSqlRawAsync(
            """
            IF COL_LENGTH('dbo.SOPInstances','UploadedAtUtc') IS NOT NULL
              UPDATE dbo.SOPInstances SET UploadedAtUtc = CreatedAtUtc WHERE UploadedAtUtc IS NULL;
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync(
            """
            IF COL_LENGTH('dbo.SOPInstances','Area') IS NULL
              ALTER TABLE dbo.SOPInstances ADD Area NVARCHAR(200) NULL;
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync(
            """
            IF COL_LENGTH('dbo.SOPInstances','Line') IS NULL
              ALTER TABLE dbo.SOPInstances ADD Line NVARCHAR(200) NULL;
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync(
            """
            IF COL_LENGTH('dbo.SOPInstances','RowVersion') IS NULL
              ALTER TABLE dbo.SOPInstances ADD RowVersion ROWVERSION;
            """, cancellationToken);
    }

    /// <summary>
    /// Existing DBs often have CHECK (Role IN ('Admin',...,'Approver3')) without Initiator. Drop those and add an extended constraint.
    /// </summary>
    public static async Task EnsureUsersRoleConstraintAllowsInitiatorAsync(SOPDbContext db, CancellationToken cancellationToken = default)
    {
        await db.Database.ExecuteSqlRawAsync(
            """
            DECLARE @constraintName sysname;
            DECLARE role_checks CURSOR LOCAL FAST_FORWARD FOR
            SELECT cc.name
            FROM sys.check_constraints AS cc
            WHERE cc.parent_object_id = OBJECT_ID(N'dbo.Users')
              AND cc.definition LIKE N'%Admin%'
              AND cc.definition LIKE N'%Approver1%'
              AND cc.definition NOT LIKE N'%Initiator%';

            OPEN role_checks;
            FETCH NEXT FROM role_checks INTO @constraintName;
            WHILE @@FETCH_STATUS = 0
            BEGIN
              DECLARE @dropSql nvarchar(500) = N'ALTER TABLE dbo.Users DROP CONSTRAINT ' + QUOTENAME(@constraintName);
              EXEC sp_executesql @dropSql;
              FETCH NEXT FROM role_checks INTO @constraintName;
            END
            CLOSE role_checks;
            DEALLOCATE role_checks;

            IF NOT EXISTS (
                SELECT 1
                FROM sys.check_constraints
                WHERE parent_object_id = OBJECT_ID(N'dbo.Users')
                  AND name = N'CK_Users_Role_DigitalSop')
            BEGIN
              ALTER TABLE dbo.Users ADD CONSTRAINT CK_Users_Role_DigitalSop CHECK (Role IN (
                N'Admin', N'Supervisor', N'Approver1', N'Approver2', N'Approver3', N'Initiator'));
            END
            """, cancellationToken);
    }

    public static async Task EnsureInitiatorUserExistsAsync(SOPDbContext db, CancellationToken cancellationToken = default)
    {
        await EnsureUsersRoleConstraintAllowsInitiatorAsync(db, cancellationToken);

        await db.Database.ExecuteSqlRawAsync(
            """
            IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Role = N'Initiator')
            INSERT INTO dbo.Users (Username, Email, PasswordHash, Role, CreatedAt)
            VALUES (N'SOP_Initiator', N'initiator@sop.local', N'initiator123', N'Initiator', SYSUTCDATETIME());
            """, cancellationToken);
    }
}
