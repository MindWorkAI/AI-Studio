namespace AIStudio.Assistants.SWOTAnalysis;

internal static class SwotAnalysisFinalizationResponseParser
{
    /// <summary>
    /// Reads the labels and the prioritized actions out of the answer of the final model.
    /// </summary>
    /// <param name="rawResponse">The answer of the model.</param>
    /// <param name="categories">The findings of the four categories, whose factor IDs the actions may name.</param>
    /// <param name="result">The labels and actions, or an empty result when reading failed.</param>
    /// <param name="issue">Why reading failed, written for the model; empty on success.</param>
    /// <returns>Whether the answer could be read.</returns>
    public static bool TryParse(
        string rawResponse,
        IReadOnlyDictionary<SwotAnalysisCategory, SwotCategoryAnalysisResult> categories,
        out SwotAnalysisFinalizationResult result,
        out string issue)
    {
        result = new();
        if (!SwotModelAnswer.TryDeserialize<SwotAnalysisFinalizationResult>(rawResponse, out var candidate, out issue))
            return false;

        Normalize(candidate);
        issue = FindIssue(candidate, SwotAnalysisFinalization.CreateFactorSummaryById(categories));
        if (issue.Length > 0)
            return false;

        result = candidate;
        return true;
    }

    private static void Normalize(SwotAnalysisFinalizationResult result)
    {
        result.MatrixHeading = result.MatrixHeading.Trim();
        result.InternalLabel = result.InternalLabel.Trim();
        result.ExternalLabel = result.ExternalLabel.Trim();
        result.PositiveLabel = result.PositiveLabel.Trim();
        result.NegativeLabel = result.NegativeLabel.Trim();
        result.StrengthsLabel = result.StrengthsLabel.Trim();
        result.WeaknessesLabel = result.WeaknessesLabel.Trim();
        result.OpportunitiesLabel = result.OpportunitiesLabel.Trim();
        result.ThreatsLabel = result.ThreatsLabel.Trim();
        result.PrioritizedActions.Label = result.PrioritizedActions.Label.Trim();
        result.PrioritizedActions.EmptyMessage = result.PrioritizedActions.EmptyMessage.Trim();
        result.PrioritizedActions.Items.RemoveAll(SwotModelAnswer.IsNull);
        foreach (var action in result.PrioritizedActions.Items)
        {
            action.Action = action.Action.Trim();
            action.Rationale = action.Rationale.Trim();
            action.AddressedFactorIds = action.AddressedFactorIds
                .Where(factorId => !string.IsNullOrWhiteSpace(factorId))
                .Select(factorId => factorId.Trim())
                .ToList();
        }
    }

    private static string FindIssue(
        SwotAnalysisFinalizationResult result,
        IReadOnlyDictionary<string, string> factorSummaryById)
    {
        var labels = new Dictionary<string, string>
        {
            ["matrix_heading"] = result.MatrixHeading,
            ["internal_label"] = result.InternalLabel,
            ["external_label"] = result.ExternalLabel,
            ["positive_label"] = result.PositiveLabel,
            ["negative_label"] = result.NegativeLabel,
            ["strengths_label"] = result.StrengthsLabel,
            ["weaknesses_label"] = result.WeaknessesLabel,
            ["opportunities_label"] = result.OpportunitiesLabel,
            ["threats_label"] = result.ThreatsLabel,
            ["prioritized_actions.label"] = result.PrioritizedActions.Label,
        };

        var emptyLabel = labels.FirstOrDefault(label => label.Value.Length == 0).Key;
        if (emptyLabel is not null)
            return $"The field {emptyLabel} is empty.";

        var actions = result.PrioritizedActions;
        if (actions.Items.Count == 0 && actions.EmptyMessage.Length == 0)
            return "prioritized_actions has neither items nor an empty_message which explains why there are none.";

        if (actions.Items.Count > 0 && actions.EmptyMessage.Length > 0)
            return "prioritized_actions has items and an empty_message at once. Leave empty_message empty when you return items.";

        for (var index = 0; index < actions.Items.Count; index++)
        {
            var action = actions.Items[index];
            if (action.Action.Length == 0)
                return $"Action {index + 1} has no action.";

            if (action.Rationale.Length == 0)
                return $"Action {index + 1} has no rationale.";

            if (action.AddressedFactorIds.Count == 0)
                return $"Action {index + 1} names no addressed_factor_ids.";

            var unknownFactorId = action.AddressedFactorIds.FirstOrDefault(factorId => !factorSummaryById.ContainsKey(factorId));
            if (unknownFactorId is not null)
                return $"Action {index + 1} names the factor ID \"{unknownFactorId}\", which no finding has. Use only the factor_id values of the findings.";
        }

        return string.Empty;
    }
}