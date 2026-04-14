using PdfEditorApi.Models;

namespace PdfEditorApi.Services;

public interface ISOPWorkflowService
{
    Task<IReadOnlyList<User>> GetUsersAsync(CancellationToken cancellationToken = default);
    Task<User?> LoginAsync(string userName, string password, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SOPInstance>> GetSopsAsync(string filter, int currentUserId, CancellationToken cancellationToken = default);
    Task<SOPInstance> CreateSopAsync(
        int currentUserId,
        string title,
        string html,
        DateTime? expiryDateUtc,
        string? area,
        string? line,
        string? editableSectionSelectorsJson,
        CancellationToken cancellationToken = default);
    Task<SOPInstance?> GetSopAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SOPInstance> SaveContentAsync(Guid id, int currentUserId, string html, CancellationToken cancellationToken = default);
    Task<SOPInstance> UpdateSOPStatus(Guid id, int currentUserId, string action, string comments, CancellationToken cancellationToken = default);
    Task<SOPInstance> WorkflowActionAsync(Guid id, int currentUserId, string action, string comments, string? signature, CancellationToken cancellationToken = default);
    Task<SOPInstance> AdminUpdateMetadataAsync(Guid id, int currentUserId, DateTime? expiryDateUtc, string? area, string? line, string? editableSectionSelectorsJson, CancellationToken cancellationToken = default);
    Task<SOPInstance> AdminReplaceVersionAsync(Guid id, int currentUserId, string html, CancellationToken cancellationToken = default);
    Task<byte[]> ExportSopsExcelAsync(int currentUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SOPAuditLog>> GetAuditLogsAsync(Guid sopInstanceId, CancellationToken cancellationToken = default);
}
