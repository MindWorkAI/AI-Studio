using System.Text.Json;
using System.Text.RegularExpressions;

namespace AIStudio.Assistants.SWOTAnalysis;

internal static class SwotAnalysisResponseParser
{
    private static readonly Regex JSON_CODE_FENCE_REGEX = new(
        pattern: """```(?:json)?\s*(?<json>\{[\s\S]*\})\s*```""",
        options: RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly JsonSerializerOptions JSON_OPTIONS = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static bool TryParse(string rawResponse, out SwotAnalysisResult result)
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

    private static bool TryDeserialize(string json, out SwotAnalysisResult result)
    {
        result = new();
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            var candidate = JsonSerializer.Deserialize<SwotAnalysisResult>(json, JSON_OPTIONS);
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

    private static void Normalize(SwotAnalysisResult result)
    {
        result.MatrixHeading = result.MatrixHeading?.Trim() ?? string.Empty;
        result.InternalLabel = result.InternalLabel?.Trim() ?? string.Empty;
        result.ExternalLabel = result.ExternalLabel?.Trim() ?? string.Empty;
        result.PositiveLabel = result.PositiveLabel?.Trim() ?? string.Empty;
        result.NegativeLabel = result.NegativeLabel?.Trim() ?? string.Empty;
        result.Strengths ??= new();
        result.Weaknesses ??= new();
        result.Opportunities ??= new();
        result.Threats ??= new();
        result.PrioritizedActions ??= new();

        foreach (var category in result.Categories)
        {
            category.Label = category.Label?.Trim() ?? string.Empty;
            category.EmptyMessage = category.EmptyMessage?.Trim() ?? string.Empty;
            category.Findings = category.Findings?
                .Where(finding => finding is not null)
                .ToList() ?? [];
            foreach (var finding in category.Findings)
            {
                finding.Summary = finding.Summary?.Trim() ?? string.Empty;
                finding.Explanation = finding.Explanation?.Trim() ?? string.Empty;
            }
        }

        result.PrioritizedActions.Label = result.PrioritizedActions.Label?.Trim() ?? string.Empty;
        result.PrioritizedActions.EmptyMessage = result.PrioritizedActions.EmptyMessage?.Trim() ?? string.Empty;
        result.PrioritizedActions.Items = result.PrioritizedActions.Items?
            .Where(action => action is not null)
            .ToList() ?? [];
        foreach (var action in result.PrioritizedActions.Items)
        {
            action.Action = action.Action?.Trim() ?? string.Empty;
            action.Rationale = action.Rationale?.Trim() ?? string.Empty;
            action.AddressedFactors = action.AddressedFactors?
                .Where(factor => !string.IsNullOrWhiteSpace(factor))
                .Select(factor => factor.Trim())
                .ToList() ?? [];
        }
    }

    private static bool IsValid(SwotAnalysisResult result)
    {
        if (string.IsNullOrWhiteSpace(result.MatrixHeading) ||
            string.IsNullOrWhiteSpace(result.InternalLabel) ||
            string.IsNullOrWhiteSpace(result.ExternalLabel) ||
            string.IsNullOrWhiteSpace(result.PositiveLabel) ||
            string.IsNullOrWhiteSpace(result.NegativeLabel))
            return false;

        if (result.Categories.Any(category =>
                string.IsNullOrWhiteSpace(category.Label) ||
                category.Findings.Count == 0 && string.IsNullOrWhiteSpace(category.EmptyMessage) ||
                category.Findings.Any(finding => string.IsNullOrWhiteSpace(finding.Summary) || string.IsNullOrWhiteSpace(finding.Explanation))))
            return false;

        return !string.IsNullOrWhiteSpace(result.PrioritizedActions.Label) &&
               (result.PrioritizedActions.Items.Count > 0 || !string.IsNullOrWhiteSpace(result.PrioritizedActions.EmptyMessage)) &&
               result.PrioritizedActions.Items.All(action =>
                   !string.IsNullOrWhiteSpace(action.Action) &&
                   !string.IsNullOrWhiteSpace(action.Rationale) &&
                   action.AddressedFactors.Count > 0);
    }
}