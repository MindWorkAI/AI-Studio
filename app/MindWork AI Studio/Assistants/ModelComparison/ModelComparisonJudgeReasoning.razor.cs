using AIStudio.Components;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// Why the judge chose so, in justified paragraphs with hyphenation.
/// </summary>
/// <remarks>
/// The judge writes its line breaks itself, and they belong to it. MudJustifiedText sets the style of its own, so
/// that it cannot keep line breaks with white-space: pre-wrap; every line of the reasoning becomes a paragraph instead.
/// </remarks>
public partial class ModelComparisonJudgeReasoning : MSGComponentBase
{
    /// <summary>
    /// Gets or sets the reasoning of the judge.
    /// </summary>
    [Parameter, EditorRequired]
    public string Reasoning { get; set; } = string.Empty;

    /// <summary>
    /// The paragraphs of the reasoning shown, without empty lines.
    /// </summary>
    private string[] paragraphs = [];

    /// <summary>
    /// The reasoning which paragraphs was built from.
    /// </summary>
    private string? shownReasoning;

    #region Overrides of ComponentBase

    protected override void OnParametersSet()
    {
        if (!string.Equals(this.shownReasoning, this.Reasoning, StringComparison.Ordinal))
        {
            this.shownReasoning = this.Reasoning;
            this.paragraphs = this.Reasoning.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        base.OnParametersSet();
    }

    #endregion
}