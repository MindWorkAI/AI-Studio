namespace AIStudio.Assistants.SWOTAnalysis;

internal static class SwotCategoryAnalysisResponseParser
{
    /// <summary>
    /// Reads the findings of one SWOT category out of a model answer.
    /// </summary>
    /// <param name="rawResponse">The answer of the model.</param>
    /// <param name="result">The findings, or an empty result when reading failed.</param>
    /// <param name="issue">Why reading failed, written for the model; empty on success.</param>
    /// <returns>Whether the answer could be read.</returns>
    public static bool TryParse(string rawResponse, out SwotCategoryAnalysisResult result, out string issue)
    {
        result = new();
        if (!SwotModelAnswer.TryDeserialize<SwotCategoryAnalysisResult>(rawResponse, out var candidate, out issue))
            return false;

        Normalize(candidate);
        issue = FindIssue(candidate);
        if (issue.Length > 0)
            return false;

        result = candidate;
        return true;
    }

    private static void Normalize(SwotCategoryAnalysisResult result)
    {
        result.EmptyMessage = result.EmptyMessage.Trim();
        result.Findings.RemoveAll(SwotModelAnswer.IsNull);
        foreach (var finding in result.Findings)
        {
            finding.Summary = finding.Summary.Trim();
            finding.Explanation = finding.Explanation.Trim();
        }
    }

    private static string FindIssue(SwotCategoryAnalysisResult result)
    {
        if (result.Findings.Count == 0 && result.EmptyMessage.Length == 0)
            return "The answer has neither findings nor an empty_message which explains why there are none.";

        if (result.Findings.Count > 0 && result.EmptyMessage.Length > 0)
            return "The answer has findings and an empty_message at once. Leave empty_message empty when you return findings.";

        for (var index = 0; index < result.Findings.Count; index++)
        {
            if (result.Findings[index].Summary.Length == 0)
                return $"Finding {index + 1} has no summary.";

            if (result.Findings[index].Explanation.Length == 0)
                return $"Finding {index + 1} has no explanation.";
        }

        return string.Empty;
    }
}