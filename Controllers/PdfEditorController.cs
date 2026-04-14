using Microsoft.AspNetCore.Mvc;
using PdfEditorApi.Services;
using PdfEditorApi.Models;
using ConvertApiDotNet;
using Aspose.Words;
using Aspose.Words.Saving;

namespace PdfEditorApi.Controllers;

[ApiController]
[Route("api/pdf")]
public sealed class PdfEditorController(
    IPdfEditorService pdfEditor,
    ILogger<PdfEditorController> logger,
    IWebHostEnvironment env) : ControllerBase
{
    private const long MaxUploadBytes = 52_428_800; // 50 MB
    private const string ConvertApiSecret = "Sku022m845tWv3UZZxF7BlubyL4H8YPo";

    [HttpPost("convert-to-html")]
    [RequestSizeLimit(MaxUploadBytes)]
    public async Task<IActionResult> ConvertToHtml(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is not { Length: > 0 })
            return BadRequest(new { error = "No file uploaded." });

        if (file.Length > MaxUploadBytes)
            return BadRequest(new { error = $"File exceeds maximum size of {MaxUploadBytes / 1_048_576} MB." });

        if (!IsPdf(file))
            return BadRequest(new { error = "Invalid file type. Upload a PDF (.pdf)." });

        try
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "pdf-editor-convertapi", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
            var sourcePdfPath = Path.Combine(tempRoot, file.FileName);
            var outputDir = Path.Combine(tempRoot, "out");
            Directory.CreateDirectory(outputDir);

            await using (var sourceFs = System.IO.File.Create(sourcePdfPath))
            {
                await file.CopyToAsync(sourceFs, cancellationToken);
            }

            byte[] docxBytes;
            string html;

            try
            {
                var convertApi = new ConvertApi(ConvertApiSecret);
                var result = await convertApi.ConvertAsync(
                    "pdf",
                    "docx",
                    new ConvertApiFileParam("File", sourcePdfPath));

                await result.Files.SaveFilesAsync(outputDir);

                var docxPath = Directory.GetFiles(outputDir, "*.docx", SearchOption.TopDirectoryOnly)
                    .OrderByDescending(System.IO.File.GetLastWriteTimeUtc)
                    .FirstOrDefault();

                if (string.IsNullOrWhiteSpace(docxPath) || !System.IO.File.Exists(docxPath))
                    throw new InvalidOperationException("ConvertAPI did not return a DOCX file.");

                docxBytes = await System.IO.File.ReadAllBytesAsync(docxPath, cancellationToken);

                using var docxInput = new MemoryStream(docxBytes);
                var doc = new Document(docxInput);
                using var htmlOut = new MemoryStream();
                var saveOptions = new HtmlSaveOptions(SaveFormat.Html)
                {
                    ExportImagesAsBase64 = true,
                    ExportHeadersFootersMode = ExportHeadersFootersMode.None
                };
                doc.Save(htmlOut, saveOptions);
                html = System.Text.Encoding.UTF8.GetString(htmlOut.ToArray());
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                    Directory.Delete(tempRoot, true);
            }

            return Ok(new
            {
                html,
                docxBase64 = Convert.ToBase64String(docxBytes)
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "PDF to HTML conversion failed for {FileName}", file.FileName);
            return Problem(
                detail: env.IsDevelopment() ? ex.ToString() : ex.Message,
                title: "PDF to HTML conversion failed",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    [HttpPost("export-to-pdf")]
    public async Task<IActionResult> ExportToPdf([FromBody] ExportToPdfRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new { error = "Request body is required." });

        try
        {
            if (string.IsNullOrWhiteSpace(request.Html))
                return BadRequest(new { error = "HTML content is required." });

            var bytes = await pdfEditor.ExportHtmlToPdfAsync(request.Html, cancellationToken);

            var stream = new MemoryStream(bytes);
            stream.Position = 0;
            var name = SanitizeFileName(request.DownloadFileName, "edited-document.pdf");
            if (!name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                name += ".pdf";
            return new FileStreamResult(stream, "application/pdf") { FileDownloadName = name };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "HTML to PDF export failed");
            return Problem(
                detail: env.IsDevelopment() ? ex.ToString() : ex.Message,
                title: "HTML to PDF export failed",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static string SanitizeFileName(string? name, string fallback)
    {
        if (string.IsNullOrWhiteSpace(name))
            return fallback;
        var trimmed = Path.GetFileName(name.Trim());
        foreach (var c in Path.GetInvalidFileNameChars())
            trimmed = trimmed.Replace(c, '_');
        return string.IsNullOrWhiteSpace(trimmed) ? fallback : trimmed;
    }

    private static bool IsPdf(IFormFile file)
    {
        var ext = Path.GetExtension(file.FileName);
        if (!string.Equals(ext, ".pdf", StringComparison.OrdinalIgnoreCase))
            return false;

        var ct = file.ContentType?.ToLowerInvariant();
        return ct is "application/pdf" or "application/octet-stream" or "application/x-pdf"
            or null or "";
    }
}
