using AIStudio.Components;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// Asks the user for the expectations, values, and goals the judge applies, and, while there are none, for the
/// confirmation that the judge may decide by its own standards.
/// </summary>
/// <remarks>
/// Both modes share this part of the judge. Without the standards of the user, the judge decides by what it learned in
/// training, with the bias of those who trained it. Like the warning about a cloud embedding, see
/// DataSourceCloudEmbeddingWarning, the user has to confirm that explicitly; IssueOf checks it when the user starts.
/// </remarks>
public partial class ModelComparisonJudgeExpectations : MSGComponentBase
{
    private new static string TA(string fallbackEN) => Tools.PluginSystem.I18N.I.TA(fallbackEN, typeof(ModelComparisonJudgeExpectations).Namespace, nameof(ModelComparisonJudgeExpectations));

    /// <summary>
    /// Gets or sets the expectations, values, and goals of the user, in their own words.
    /// </summary>
    [Parameter]
    public string Expectations { get; set; } = string.Empty;

    [Parameter]
    public EventCallback<string> ExpectationsChanged { get; set; }

    /// <summary>
    /// Gets or sets whether the user confirmed that the judge may decide by its own standards. It counts only while
    /// there are no expectations.
    /// </summary>
    [Parameter]
    public bool OwnStandardsAccepted { get; set; }

    [Parameter]
    public EventCallback<bool> OwnStandardsAcceptedChanged { get; set; }

    /// <summary>
    /// Gets or sets whether the switch shows what is missing: only once the user tried to start, so that it does not
    /// complain before the user had a chance to fill anything in.
    /// </summary>
    [Parameter]
    public bool ShowsIssue { get; set; }

    /// <summary>
    /// Checks whether the judge may start: with expectations of the user, or with the confirmation that it may decide
    /// by its own standards.
    /// </summary>
    /// <param name="expectations">The expectations, values, and goals of the user.</param>
    /// <param name="ownStandardsAccepted">Whether the user confirmed that the judge may decide by its own standards.</param>
    /// <returns>What is missing, or null when the judge may start.</returns>
    public static string? IssueOf(string expectations, bool ownStandardsAccepted) => string.IsNullOrWhiteSpace(expectations) && !ownStandardsAccepted
        ? TA("Please describe your expectations, values, and goals to the judge, or confirm that an AI may decide for you what is good and right.")
        : null;

    private bool HasExpectations => !string.IsNullOrWhiteSpace(this.Expectations);

    private Task OnExpectationsChanged(string? text) => this.ExpectationsChanged.InvokeAsync(text ?? string.Empty);

    private string? ValidatingAcceptance(bool accepted) => this.ShowsIssue ? IssueOf(this.Expectations, accepted) : null;
}