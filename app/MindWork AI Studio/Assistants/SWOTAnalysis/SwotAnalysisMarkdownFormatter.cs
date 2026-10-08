using System.Text;

namespace AIStudio.Assistants.SWOTAnalysis;

internal static class SwotAnalysisMarkdownFormatter
{
    public static string Format(SwotAnalysisResult result)
    {
        var markdown = new StringBuilder();

        AppendHeading(markdown, result.MatrixHeading);
        markdown.AppendLine($"| | {EscapeTableCell(result.PositiveLabel)} | {EscapeTableCell(result.NegativeLabel)} |");
        markdown.AppendLine("|---|---|---|");
        markdown.AppendLine($"| **{EscapeTableCell(result.InternalLabel)}** | {FormatCategoryCell(result.Strengths)} | {FormatCategoryCell(result.Weaknesses)} |");
        markdown.AppendLine($"| **{EscapeTableCell(result.ExternalLabel)}** | {FormatCategoryCell(result.Opportunities)} | {FormatCategoryCell(result.Threats)} |");
        markdown.AppendLine();

        foreach (var category in result.Categories)
        {
            AppendHeading(markdown, category.Label);
            if (category.Findings.Count == 0)
                markdown.AppendLine(Tools.Markdown.EscapeInlineText(category.EmptyMessage));
            else
                foreach (var finding in category.Findings)
                    markdown.AppendLine($"- **{Tools.Markdown.EscapeInlineText(finding.Summary)}** — {Tools.Markdown.EscapeInlineText(finding.Explanation)}");

            markdown.AppendLine();
        }

        AppendHeading(markdown, result.PrioritizedActions.Label);
        if (result.PrioritizedActions.Items.Count == 0)
            markdown.AppendLine(Tools.Markdown.EscapeInlineText(result.PrioritizedActions.EmptyMessage));
        else
            for (var index = 0; index < result.PrioritizedActions.Items.Count; index++)
            {
                var action = result.PrioritizedActions.Items[index];
                var factors = string.Join("; ", action.AddressedFactors.Select(Tools.Markdown.EscapeInlineText));
                markdown.AppendLine($"{index + 1}. **{Tools.Markdown.EscapeInlineText(action.Action)}** — {Tools.Markdown.EscapeInlineText(action.Rationale)}  ");
                markdown.AppendLine($"   *{factors}*");
            }

        return markdown.ToString().TrimEnd();
    }

    private static void AppendHeading(StringBuilder markdown, string heading)
    {
        markdown.Append("## ");
        markdown.AppendLine(Tools.Markdown.EscapeInlineText(heading));
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