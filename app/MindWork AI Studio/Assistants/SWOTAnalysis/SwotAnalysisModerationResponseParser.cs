namespace AIStudio.Assistants.SWOTAnalysis;

internal static class SwotAnalysisModerationResponseParser
{
    /// <summary>
    /// Reads the answer of the moderator and checks that the result can be assembled from it.
    /// </summary>
    /// <param name="rawResponse">The answer of the model.</param>
    /// <param name="result">The answer, or an empty one when reading failed.</param>
    /// <param name="issue">Why reading failed, written for the model; empty on success.</param>
    /// <returns>Whether the answer could be read.</returns>
    public static bool TryParse(string rawResponse, out SwotModerationResult result, out string issue)
    {
        result = new();
        if (!SwotModelAnswer.TryDeserialize<SwotModerationResult>(rawResponse, out var candidate, out issue))
            return false;

        Normalize(candidate);
        issue = FindIssue(candidate);
        if (issue.Length > 0)
            return false;

        result = candidate;
        return true;
    }

    private static void Normalize(SwotModerationResult result)
    {
        result.MatrixHeading = result.MatrixHeading.Trim();
        result.InternalLabel = result.InternalLabel.Trim();
        result.ExternalLabel = result.ExternalLabel.Trim();
        result.PositiveLabel = result.PositiveLabel.Trim();
        result.NegativeLabel = result.NegativeLabel.Trim();

        foreach (var category in Enum.GetValues<SwotAnalysisCategory>().Select(result.CategoryOf))
        {
            category.Label = category.Label.Trim();
            category.EmptyMessage = category.EmptyMessage.Trim();
            category.Findings.RemoveAll(SwotModelAnswer.IsNull);
            foreach (var finding in category.Findings)
            {
                finding.FactorId = finding.FactorId.Trim();
                finding.Summary = finding.Summary.Trim();
                finding.Explanation = finding.Explanation.Trim();
            }
        }

        var notes = result.ModerationNotes;
        notes.Label = notes.Label.Trim();
        notes.EmptyMessage = notes.EmptyMessage.Trim();
        notes.Items.RemoveAll(SwotModelAnswer.IsNull);
        foreach (var note in notes.Items)
        {
            note.Change = note.Change.Trim();
            note.Reason = note.Reason.Trim();
        }

        result.OverallAssessment.Label = result.OverallAssessment.Label.Trim();
        result.OverallAssessment.Text = result.OverallAssessment.Text.Trim();

        var actions = result.PrioritizedActions;
        actions.Label = actions.Label.Trim();
        actions.EmptyMessage = actions.EmptyMessage.Trim();
        actions.Items.RemoveAll(SwotModelAnswer.IsNull);
        foreach (var action in actions.Items)
        {
            action.Action = action.Action.Trim();
            action.Rationale = action.Rationale.Trim();
            action.AddressedFactorIds = action.AddressedFactorIds
                .Where(factorId => !string.IsNullOrWhiteSpace(factorId))
                .Select(factorId => factorId.Trim())
                .ToList();
        }
    }

    private static string FindIssue(SwotModerationResult result)
    {
        var fields = new Dictionary<string, string>
        {
            ["matrix_heading"] = result.MatrixHeading,
            ["internal_label"] = result.InternalLabel,
            ["external_label"] = result.ExternalLabel,
            ["positive_label"] = result.PositiveLabel,
            ["negative_label"] = result.NegativeLabel,
            ["strengths.label"] = result.Strengths.Label,
            ["weaknesses.label"] = result.Weaknesses.Label,
            ["opportunities.label"] = result.Opportunities.Label,
            ["threats.label"] = result.Threats.Label,
            ["moderation_notes.label"] = result.ModerationNotes.Label,
            ["overall_assessment.label"] = result.OverallAssessment.Label,
            ["overall_assessment.text"] = result.OverallAssessment.Text,
            ["prioritized_actions.label"] = result.PrioritizedActions.Label,
        };

        var emptyField = fields.FirstOrDefault(field => field.Value.Length == 0).Key;
        if (emptyField is not null)
            return $"The field {emptyField} is empty.";

        var factorIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var category in Enum.GetValues<SwotAnalysisCategory>())
        {
            var issue = FindIssue(category, result.CategoryOf(category), factorIds);
            if (issue.Length > 0)
                return issue;
        }

        var notes = result.ModerationNotes;
        var notesIssue = FindListIssue("moderation_notes", notes.Items.Count, notes.EmptyMessage);
        if (notesIssue.Length > 0)
            return notesIssue;

        for (var index = 0; index < notes.Items.Count; index++)
        {
            if (notes.Items[index].Change.Length == 0)
                return $"Note {index + 1} of moderation_notes has no change.";

            if (notes.Items[index].Reason.Length == 0)
                return $"Note {index + 1} of moderation_notes has no reason.";
        }

        var actions = result.PrioritizedActions;
        var actionsIssue = FindListIssue("prioritized_actions", actions.Items.Count, actions.EmptyMessage);
        if (actionsIssue.Length > 0)
            return actionsIssue;

        for (var index = 0; index < actions.Items.Count; index++)
        {
            var action = actions.Items[index];
            if (action.Action.Length == 0)
                return $"Action {index + 1} has no action.";

            if (action.Rationale.Length == 0)
                return $"Action {index + 1} has no rationale.";

            if (action.AddressedFactorIds.Count == 0)
                return $"Action {index + 1} names no addressed_factor_ids.";

            var unknownFactorId = action.AddressedFactorIds.FirstOrDefault(factorId => !factorIds.Contains(factorId));
            if (unknownFactorId is not null)
                return $"Action {index + 1} names the factor ID \"{unknownFactorId}\", which no final finding has. Use only the factor_id values of the final findings.";
        }

        return string.Empty;
    }

    /// <summary>
    /// Checks the findings of one category, and collects their factor IDs.
    /// </summary>
    /// <param name="category">The category.</param>
    /// <param name="moderatedCategory">The category as the moderator returned it.</param>
    /// <param name="factorIds">The factor IDs found so far, to which the IDs of this category are added.</param>
    /// <returns>The first issue, or an empty string.</returns>
    private static string FindIssue(SwotAnalysisCategory category, SwotModeratedCategory moderatedCategory, HashSet<string> factorIds)
    {
        var name = category.JsonName();
        var listIssue = FindListIssue(name, moderatedCategory.Findings.Count, moderatedCategory.EmptyMessage);
        if (listIssue.Length > 0)
            return listIssue;

        var prefix = category.FactorIdPrefix();
        for (var index = 0; index < moderatedCategory.Findings.Count; index++)
        {
            var finding = moderatedCategory.Findings[index];
            if (finding.Summary.Length == 0)
                return $"Finding {index + 1} of {name} has no summary.";

            if (finding.Explanation.Length == 0)
                return $"Finding {index + 1} of {name} has no explanation.";

            if (!finding.FactorId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return $"Finding {index + 1} of {name} has the factor_id \"{finding.FactorId}\", which does not start with \"{prefix}\".";

            if (!factorIds.Add(finding.FactorId))
                return $"The factor_id \"{finding.FactorId}\" is used more than once. Give every finding a factor_id of its own.";
        }

        return string.Empty;
    }

    /// <summary>
    /// Checks that a list either has items or explains in its empty message why it has none.
    /// </summary>
    private static string FindListIssue(string name, int itemCount, string emptyMessage)
    {
        if (itemCount == 0 && emptyMessage.Length == 0)
            return $"{name} has no items and no empty_message which explains why.";

        if (itemCount > 0 && emptyMessage.Length > 0)
            return $"{name} has items and an empty_message at once. Leave empty_message empty when you return items.";

        return string.Empty;
    }
}