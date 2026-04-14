using Microsoft.AspNetCore.Mvc;
using PdfEditorApi.Models;
using PdfEditorApi.Services;

namespace PdfEditorApi.Controllers;

[ApiController]
[Route("api/sop")]
public sealed class SOPController(ISOPWorkflowService workflow) : ControllerBase
{
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new { error = "Request body is required." });

        var user = await workflow.LoginAsync(request.UserName, request.Password, cancellationToken);
        if (user is null)
            return Unauthorized(new { error = "Invalid username or password." });

        return Ok(new
        {
            id = user.Id,
            userName = user.UserName,
            displayName = string.IsNullOrWhiteSpace(user.DisplayName) ? user.UserName : user.DisplayName,
            role = user.Role.ToString()
        });
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers(CancellationToken cancellationToken)
    {
        var users = await workflow.GetUsersAsync(cancellationToken);
        return Ok(users);
    }

    [HttpGet("export/excel")]
    public async Task<IActionResult> ExportExcel([FromQuery] int currentUserId, CancellationToken cancellationToken)
    {
        if (currentUserId <= 0)
            return BadRequest(new { error = "currentUserId is required." });
        try
        {
            var bytes = await workflow.ExportSopsExcelAsync(currentUserId, cancellationToken);
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "sop-records.xlsx");
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetSops([FromQuery] string filter, [FromQuery] int currentUserId, CancellationToken cancellationToken)
    {
        var sops = await workflow.GetSopsAsync(filter, currentUserId, cancellationToken);
        return Ok(sops);
    }

    [HttpPost]
    public async Task<IActionResult> CreateSop([FromBody] CreateSopRequest request, CancellationToken cancellationToken)
    {
        if (request is null || request.CurrentUserId <= 0)
            return BadRequest(new { error = "CurrentUserId is required." });

        try
        {
            var created = await workflow.CreateSopAsync(
                request.CurrentUserId,
                request.Title,
                request.Html,
                request.ExpiryDateUtc,
                request.Area,
                request.Line,
                request.EditableSectionSelectorsJson,
                cancellationToken);
            return Ok(created);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetSop(Guid id, CancellationToken cancellationToken)
    {
        var sop = await workflow.GetSopAsync(id, cancellationToken);
        return sop is null ? NotFound(new { error = "SOP not found." }) : Ok(sop);
    }

    [HttpGet("{id:guid}/audit-logs")]
    public async Task<IActionResult> GetAuditLogs(Guid id, CancellationToken cancellationToken)
    {
        var logs = await workflow.GetAuditLogsAsync(id, cancellationToken);
        return Ok(logs);
    }

    [HttpPut("{id:guid}/admin")]
    public async Task<IActionResult> AdminUpdate(Guid id, [FromBody] AdminUpdateSopRequest request, CancellationToken cancellationToken)
    {
        if (request is null || request.CurrentUserId <= 0)
            return BadRequest(new { error = "CurrentUserId is required." });
        try
        {
            var updated = await workflow.AdminUpdateMetadataAsync(
                id,
                request.CurrentUserId,
                request.ExpiryDateUtc,
                request.Area,
                request.Line,
                request.EditableSectionSelectorsJson,
                cancellationToken);
            return Ok(updated);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/admin/version")]
    public async Task<IActionResult> AdminReplaceVersion(Guid id, [FromBody] AdminReplaceVersionRequest request, CancellationToken cancellationToken)
    {
        if (request is null || request.CurrentUserId <= 0)
            return BadRequest(new { error = "CurrentUserId is required." });
        try
        {
            var updated = await workflow.AdminReplaceVersionAsync(id, request.CurrentUserId, request.Html ?? string.Empty, cancellationToken);
            return Ok(updated);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/workflow")]
    public async Task<IActionResult> Workflow(Guid id, [FromBody] WorkflowActionRequest request, CancellationToken cancellationToken)
    {
        if (request is null || request.CurrentUserId <= 0)
            return BadRequest(new { error = "CurrentUserId is required." });
        try
        {
            var updated = await workflow.WorkflowActionAsync(
                id,
                request.CurrentUserId,
                request.Action,
                request.Comments ?? string.Empty,
                request.Signature,
                cancellationToken);
            return Ok(updated);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}/content")]
    public async Task<IActionResult> SaveContent(Guid id, [FromBody] SaveSOPContentRequest request, CancellationToken cancellationToken)
    {
        if (request is null || request.CurrentUserId <= 0)
            return BadRequest(new { error = "CurrentUserId is required." });

        try
        {
            var updated = await workflow.SaveContentAsync(id, request.CurrentUserId, request.Html ?? string.Empty, cancellationToken);
            return Ok(updated);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateSOPStatusRequest request, CancellationToken cancellationToken)
    {
        if (request is null || request.CurrentUserId <= 0)
            return BadRequest(new { error = "CurrentUserId is required." });

        try
        {
            var updated = await workflow.UpdateSOPStatus(id, request.CurrentUserId, request.Action, request.Comments, cancellationToken);
            return Ok(updated);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
