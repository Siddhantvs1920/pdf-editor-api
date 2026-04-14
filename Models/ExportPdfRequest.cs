namespace PdfEditorApi.Models;

public sealed class ExportPdfRequest
{
    public string Mode { get; set; } = "html";
    public string Html { get; set; } = string.Empty;
    public string SourcePdfBase64 { get; set; } = string.Empty;
    public List<PdfFormFieldValueInput> FormFields { get; set; } = [];
}

public sealed class PdfFormFieldValueInput
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
