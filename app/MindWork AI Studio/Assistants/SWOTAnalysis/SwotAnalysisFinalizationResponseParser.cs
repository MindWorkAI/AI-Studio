using System.Text.Json;
using System.Text.RegularExpressions;

namespace AIStudio.Assistants.SWOTAnalysis;

internal static class SwotAnalysisFinalizationResponseParser
{
    private static readonly Regex JSON_CODE_FENCE_REGEX = new(
        pattern: """```(?:json)?\s*(?<json>\{[\s\S]*\})\s*```""",
        options: RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly JsonSerializerOptions JSON_OPTIONS = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static bool TryParse(
        string rawResponse,
        IReadOnlyDictionary<SwotAnalysisCategory, SwotCategoryAnalysisResult> categories,
        out SwotAnalysisFinalizationResult result)
    {
        var factorSummaryById = SwotAnalysisFinalization.CreateFactorSummaryById(categories);

        result = new();
        if (TryDeserialize(rawResponse, factorSummaryById, out result))
            return true;

        var codeFenceMatch = JSON_CODE_FENCE_REGEX.Match(rawResponse);
        if (codeFenceMatch.Success && TryDeserialize(codeFenceMatch.Groups["json"].Value, factorSummaryById, out result))
            return true;

        var firstBrace = rawResponse.IndexOf('{');
        var lastBrace = rawResponse.LastIndexOf('}');
        return firstBrace >= 0 && lastBrace > firstBrace &&
               TryDeserialize(rawResponse[firstBrace..(lastBrace + 1)], factorSummaryById, out result);
    }

    private static bool TryDeserialize(
        string json,
        IReadOnlyDictionary<string, string> factorSummaryById,
        out SwotAnalysisFinalizationResult result)
    {
        result = new();
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            var candidate = JsonSerializer.Deserialize<SwotAnalysisFinalizationResult>(json, JSON_OPTIONS);
            if (candidate is null)
                return false;

            Normalize(candidate);
            if (!IsValid(candidate, factorSummaryById))
                return false;

            result = candidate;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static void Normalize(SwotAnalysisFinalizationResult result)
    {
        result.MatrixHeading = result.MatrixHeading?.Trim() ?? string.Empty;
        result.InternalLabel = result.InternalLabel?.Trim() ?? string.Empty;
        result.ExternalLabel = result.ExternalLabel?.Trim() ?? string.Empty;
        result.PositiveLabel = result.PositiveLabel?.Trim() ?? string.Empty;
        result.NegativeLabel = result.NegativeLabel?.Trim() ?? string.Empty;
        result.StrengthsLabel = result.StrengthsLabel?.Trim() ?? string.Empty;
        result.WeaknessesLabel = result.WeaknessesLabel?.Trim() ?? string.Empty;
        result.OpportunitiesLabel = result.OpportunitiesLabel?.Trim() ?? string.Empty;
        result.ThreatsLabel = result.ThreatsLabel?.Trim() ?? string.Empty;
        result.PrioritizedActions ??= new();
        result.PrioritizedActions.Label = result.PrioritizedActions.Label?.Trim() ?? string.Empty;
        result.PrioritizedActions.EmptyMessage = result.PrioritizedActions.EmptyMessage?.Trim() ?? string.Empty;
        result.PrioritizedActions.Items = result.PrioritizedActions.Items?
            .Where(action => action is not null)
            .ToList() ?? [];

        foreach (var action in result.PrioritizedActions.Items)
        {
            action.Action = action.Action?.Trim() ?? string.Empty;
            action.Rationale = action.Rationale?.Trim() ?? string.Empty;
            action.AddressedFactorIds = action.AddressedFactorIds?
                .Where(factorId => !string.IsNullOrWhiteSpace(factorId))
                .Select(factorId => factorId.Trim())
                .ToList() ?? [];
        }
    }

    private static bool IsValid(
        SwotAnalysisFinalizationResult result,
        IReadOnlyDictionary<string, string> factorSummaryById)
    {
        if (new[]
            {
                result.MatrixHeading,
                result.InternalLabel,
                result.ExternalLabel,
                result.PositiveLabel,
                result.NegativeLabel,
                result.StrengthsLabel,
                result.WeaknessesLabel,
                result.OpportunitiesLabel,
                result.ThreatsLabel,
                result.PrioritizedActions.Label,
            }.Any(string.IsNullOrWhiteSpace))
            return false;

        return (result.PrioritizedActions.Items.Count == 0 && !string.IsNullOrWhiteSpace(result.PrioritizedActions.EmptyMessage) ||
                result.PrioritizedActions.Items.Count > 0 && string.IsNullOrWhiteSpace(result.PrioritizedActions.EmptyMessage)) &&
               result.PrioritizedActions.Items.All(action =>
                   !string.IsNullOrWhiteSpace(action.Action) &&
                   !string.IsNullOrWhiteSpace(action.Rationale) &&
                   action.AddressedFactorIds.Count > 0 &&
                   action.AddressedFactorIds.All(factorSummaryById.ContainsKey));
    }
}