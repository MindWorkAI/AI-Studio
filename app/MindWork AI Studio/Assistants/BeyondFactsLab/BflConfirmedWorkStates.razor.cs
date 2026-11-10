using AIStudio.Components;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// The work states the person confirmed in a preparation of the BeyondFacts Lab, beside the steps.
/// </summary>
/// <remarks>
/// They are what the lab builds on in the later steps, so the person sees them while working on any step, with the
/// outdated ones marked.
/// </remarks>
public partial class BflConfirmedWorkStates : MSGComponentBase
{
    /// <summary>
    /// Gets or sets the work states of the preparation, by their steps.
    /// </summary>
    [Parameter, EditorRequired]
    public IReadOnlyDictionary<BflStep, BflWorkState> WorkStates { get; set; } = new Dictionary<BflStep, BflWorkState>();

    /// <summary>
    /// Gets or sets the forms of the preparation, by their steps, which tell whether a work state is outdated.
    /// </summary>
    [Parameter, EditorRequired]
    public IReadOnlyDictionary<BflStep, BflStepFormData> Forms { get; set; } = new Dictionary<BflStep, BflStepFormData>();

    /// <summary>
    /// Gets or sets further CSS classes of the area.
    /// </summary>
    [Parameter]
    public string Class { get; set; } = string.Empty;

    private IReadOnlyList<BflStep> ConfirmedSteps => [.. BflStep.All.Where(step => this.WorkStates.GetValueOrDefault(step)?.Confirmed is not null)];
}