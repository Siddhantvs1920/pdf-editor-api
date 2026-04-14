namespace PdfEditorApi.Models;

public sealed class ConvertPdfResponse
{
    public string Mode { get; set; } = "html";
    public string Html { get; set; } = string.Empty;
    public string ExtractedText { get; set; } = string.Empty;
    public string SourcePdfBase64 { get; set; } = string.Empty;
    public List<PdfFormFieldDto> FormFields { get; set; } = [];
}

public sealed class PdfFormFieldDto
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string FieldType { get; set; } = string.Empty;
}
