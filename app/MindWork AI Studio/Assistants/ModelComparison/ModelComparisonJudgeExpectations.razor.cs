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
/// DataSourceCloudEmbeddingWarning, the user has to confirm that explicitly. The switch is a field of the form around
/// this component, which checks it when the user starts; IssueOf checks the same for code outside the form.
/// </remarks>
public partial class ModelComparisonJudgeExpectations : MSGComponentBase
{
    private new static string TA(string fallbackEN) => Tools.PluginSystem.I18N.I.TA(fallbackEN, typeof(ModelComparisonJudgeExpectations).Namespace, nameof(ModelComparisonJudgeExpectations));

    /// <summary>
    /// How long the field waits after the last keystroke before it passes the expectations on. The switch appears or
    /// disappears with them, so it follows the typing without flickering.
    /// </summary>
    private const double DEBOUNCE_MILLISECONDS = 500;

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

    private MudTextField<string>? expectationsField;

    /// <summary>
    /// Whether there were expectations the last time the parameters arrived, i.e., whether the switch was hidden; null
    /// before they arrived the first time.
    /// </summary>
    private bool? hadExpectations;

    /// <summary>
    /// Whether the switch has just gone away, so that the form has to forget what it found fault with in it.
    /// </summary>
    private bool switchWentAway;

    #region Overrides of ComponentBase

    protected override void OnParametersSet()
    {
        if (this.hadExpectations is false && this.HasExpectations)
            this.switchWentAway = true;

        this.hadExpectations = this.HasExpectations;
        base.OnParametersSet();
    }

    /// <remarks>
    /// A MudForm gathers the issues of its fields only when one of them is checked. Without the switch, its issue
    /// would stay in the list of issues until the next check. Checking the field of the expectations after the render
    /// which removed the switch lets the form gather the issues anew, from the fields which are still there.
    /// </remarks>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (this.switchWentAway)
        {
            this.switchWentAway = false;
            if (this.expectationsField is not null)
                await this.expectationsField.ValidateAsync();
        }

        await base.OnAfterRenderAsync(firstRender);
    }

    #endregion

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

    /// <remarks>
    /// The form shows the issue of a field the user has not touched only once it checks all of them, i.e., when the
    /// user starts. So the check needs no flag of its own for that, unlike a field whose value only arrives later.
    /// </remarks>
    private string? ValidatingAcceptance(bool accepted) => IssueOf(this.Expectations, accepted);
}