using System.Text;

namespace AIStudio.Assistants.SWOTAnalysis;

/// <summary>
/// Writes a SWOT analysis as Markdown, the canonical text which providers, copying, exporting, and a
/// chat get.
/// </summary>
/// <remarks>
/// The order is the order of the result view: the matrix, the overall assessment, and the actions
/// first, which is what a reader needs for a decision; then the details of each category, and last
/// what the moderator changed.
/// </remarks>
internal static class SwotAnalysisMarkdownFormatter
{
    public static string Format(SwotAnalysisResult result)
    {
        var markdown = new StringBuilder();

        AppendHeading(markdown, result.MatrixHeading);
        markdown.AppendLineLF($"| | {EscapeTableCell(result.PositiveLabel)} | {EscapeTableCell(result.NegativeLabel)} |");
        markdown.AppendLineLF("|---|---|---|");
        markdown.AppendLineLF($"| **{EscapeTableCell(result.InternalLabel)}** | {FormatCategoryCell(result.Strengths)} | {FormatCategoryCell(result.Weaknesses)} |");
        markdown.AppendLineLF($"| **{EscapeTableCell(result.ExternalLabel)}** | {FormatCategoryCell(result.Opportunities)} | {FormatCategoryCell(result.Threats)} |");
        markdown.AppendLineLF();

        AppendHeading(markdown, result.OverallAssessment.Label);
        foreach (var paragraph in result.OverallAssessment.Paragraphs)
        {
            markdown.AppendLineLF(Markdown.EscapeInlineText(paragraph));
            markdown.AppendLineLF();
        }

        AppendHeading(markdown, result.PrioritizedActions.Label);
        if (result.PrioritizedActions.Items.Count == 0)
            markdown.AppendLineLF(Markdown.EscapeInlineText(result.PrioritizedActions.EmptyMessage));
        else
            for (var index = 0; index < result.PrioritizedActions.Items.Count; index++)
            {
                var action = result.PrioritizedActions.Items[index];
                var factors = string.Join("; ", action.AddressedFactors.Select(Markdown.EscapeInlineText));
                markdown.AppendLineLF($"{index + 1}. **{Markdown.EscapeInlineText(action.Action)}** — {Markdown.EscapeInlineText(action.Rationale)}  ");
                markdown.AppendLineLF($"   *{factors}*");
            }

        markdown.AppendLineLF();
        foreach (var category in result.Categories)
        {
            AppendHeading(markdown, category.Label);
            if (category.Findings.Count == 0)
                markdown.AppendLineLF(Markdown.EscapeInlineText(category.EmptyMessage));
            else
                foreach (var finding in category.Findings)
                    markdown.AppendLineLF($"- **{Markdown.EscapeInlineText(finding.Summary)}** — {Markdown.EscapeInlineText(finding.Explanation)}");

            markdown.AppendLineLF();
        }

        AppendHeading(markdown, result.ModerationNotes.Label);
        if (result.ModerationNotes.Items.Count == 0)
            markdown.AppendLineLF(Markdown.EscapeInlineText(result.ModerationNotes.EmptyMessage));
        else
            foreach (var note in result.ModerationNotes.Items)
                markdown.AppendLineLF($"- **{Markdown.EscapeInlineText(note.Change)}** — {Markdown.EscapeInlineText(note.Reason)}");

        return markdown.ToString().TrimEnd();
    }

    private static void AppendHeading(StringBuilder markdown, string heading)
    {
        markdown.Append("## ");
        markdown.AppendLineLF(Markdown.EscapeInlineText(heading));
    }

    private static string FormatCategoryCell(SwotCategory category)
    {
        var content = category.Findings.Count == 0
            ? EscapeTableCell(category.EmptyMessage)
            : string.Join("; ", category.Findings.Select(finding => EscapeTableCell(finding.Summary)));

        return $"**{EscapeTableCell(category.Label)}:** {content}";
    }

    private static string EscapeTableCell(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("|", "\\|", StringComparison.Ordinal)
        .Replace('\r', ' ')
        .Replace('\n', ' ')
        .Trim();
}