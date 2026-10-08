using System.Text.Json;
using System.Text.RegularExpressions;

namespace AIStudio.Assistants.SWOTAnalysis;

internal static class SwotCategoryAnalysisResponseParser
{
    private static readonly Regex JSON_CODE_FENCE_REGEX = new(
        pattern: """```(?:json)?\s*(?<json>\{[\s\S]*\})\s*```""",
        options: RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly JsonSerializerOptions JSON_OPTIONS = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static bool TryParse(string rawResponse, out SwotCategoryAnalysisResult result)
    {
        result = new();

        if (TryDeserialize(rawResponse, out result))
            return true;

        var codeFenceMatch = JSON_CODE_FENCE_REGEX.Match(rawResponse);
        if (codeFenceMatch.Success && TryDeserialize(codeFenceMatch.Groups["json"].Value, out result))
            return true;

        var firstBrace = rawResponse.IndexOf('{');
        var lastBrace = rawResponse.LastIndexOf('}');
        return firstBrace >= 0 && lastBrace > firstBrace && TryDeserialize(rawResponse[firstBrace..(lastBrace + 1)], out result);
    }

    private static bool TryDeserialize(string json, out SwotCategoryAnalysisResult result)
    {
        result = new();
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            var candidate = JsonSerializer.Deserialize<SwotCategoryAnalysisResult>(json, JSON_OPTIONS);
            if (candidate is null)
                return false;

            Normalize(candidate);
            if (!IsValid(candidate))
                return false;

            result = candidate;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static void Normalize(SwotCategoryAnalysisResult result)
    {
        result.EmptyMessage = result.EmptyMessage?.Trim() ?? string.Empty;
        result.Findings = result.Findings?
            .Where(finding => finding is not null)
            .ToList() ?? [];
        foreach (var finding in result.Findings)
        {
            finding.Summary = finding.Summary?.Trim() ?? string.Empty;
            finding.Explanation = finding.Explanation?.Trim() ?? string.Empty;
        }
    }

    private static bool IsValid(SwotCategoryAnalysisResult result) =>
        (result.Findings.Count == 0 && !string.IsNullOrWhiteSpace(result.EmptyMessage) ||
         result.Findings.Count > 0 && string.IsNullOrWhiteSpace(result.EmptyMessage)) &&
        result.Findings.All(finding =>
            !string.IsNullOrWhiteSpace(finding.Summary) &&
            !string.IsNullOrWhiteSpace(finding.Explanation));
}