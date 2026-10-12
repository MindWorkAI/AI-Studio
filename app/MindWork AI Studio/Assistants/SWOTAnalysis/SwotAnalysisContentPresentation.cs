using System.Text.Json;

using AIStudio.Chat;

namespace AIStudio.Assistants.SWOTAnalysis;

internal static class SwotAnalysisContentPresentation
{
    public static ContentPresentation Create(SwotAnalysisResult result) =>
        ContentPresentation.Create(ContentPresentationKind.SWOT_ANALYSIS, result);

    public static bool TryRead(ContentText content, out SwotAnalysisResult result)
    {
        result = new();
        var presentation = content.Presentation;
        if (presentation is null ||
            presentation.Kind is not ContentPresentationKind.SWOT_ANALYSIS ||
            presentation.Data is not { } data ||
            data.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            return false;

        return SwotAnalysisResponseParser.TryParse(data.GetRawText(), out result);
    }
}