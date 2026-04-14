using System.Text.Json;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using System.Linq;

namespace PdfEditorApi.Services;

public static class EditableHtmlMerger
{
    public static string Merge(string templateHtml, string submittedHtml, string editableSelectorsJson)
    {
        var selectors = ParseSelectors(editableSelectorsJson);
        var parser = new HtmlParser();
        using var templateDoc = parser.ParseDocument(WrapIfNeeded(templateHtml));
        using var submittedDoc = parser.ParseDocument(WrapIfNeeded(submittedHtml));

        var tBody = templateDoc.Body ?? templateDoc.DocumentElement;
        var sBody = submittedDoc.Body ?? submittedDoc.DocumentElement;
        if (tBody is null || sBody is null)
            return templateHtml;

        if (selectors.Count == 0 || selectors.Contains("*"))
        {
            tBody.InnerHtml = sBody.InnerHtml;
            return tBody.InnerHtml;
        }

        foreach (var sel in selectors)
        {
            if (string.IsNullOrWhiteSpace(sel) || sel == "*")
                continue;
            var tNodes = tBody.QuerySelectorAll(sel).ToList();
            var sNodes = sBody.QuerySelectorAll(sel).ToList();
            var n = Math.Min(tNodes.Count, sNodes.Count);
            for (var i = 0; i < n; i++)
                tNodes[i].InnerHtml = sNodes[i].InnerHtml;
        }

        return tBody.InnerHtml;
    }

    private static List<string> ParseSelectors(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "[]")
            return [];
        try
        {
            var arr = JsonSerializer.Deserialize<string[]>(json);
            return arr?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToList() ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static string WrapIfNeeded(string html)
    {
        var h = html.Trim();
        if (h.Contains("<html", StringComparison.OrdinalIgnoreCase))
            return h;
        return "<!DOCTYPE html><html><head><meta charset=\"utf-8\"></head><body>" + h + "</body></html>";
    }
}
