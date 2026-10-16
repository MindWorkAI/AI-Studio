using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// A stepper that leaves the step navigation to the application instead of the user.
/// </summary>
/// <remarks>
/// MudBlazor renders Previous, Next, Skip, and Complete buttons by default. AI Studio drives its
/// steppers from application state — a running build, or an install flow that advances when each
/// step succeeds — so those buttons have nothing to do and would only look broken when clicked.
/// This component removes them once instead of once per assistant, and carries the shared step
/// colors so the steppers stay visually consistent.
/// </remarks>
public partial class MudStepperWithoutActions : ComponentBase
{
    /// <summary>
    /// Gets or sets the step the stepper points at.
    /// </summary>
    [Parameter]
    public int ActiveIndex { get; set; }

    /// <summary>
    /// Gets or sets the callback raised when the active step changed.
    /// </summary>
    [Parameter]
    public EventCallback<int> ActiveIndexChanged { get; set; }

    /// <summary>
    /// Gets or sets whether the user must not change the active step.
    /// </summary>
    /// <remarks>
    /// Set this when the displayed process runs on its own. The step headers stay visible, but
    /// clicking them no longer moves the stepper away from the step the application selected.
    /// </remarks>
    [Parameter]
    public bool ReadOnly { get; set; }

    /// <summary>
    /// Gets or sets additional CSS classes for the stepper.
    /// </summary>
    [Parameter]
    public string Class { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the steps to render.
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// The marker class that lets app.css hide the action bar MudBlazor renders around the actions.
    /// </summary>
    private const string MARKER_CLASS = "mud-stepper-without-actions";

    private string Classname => string.IsNullOrWhiteSpace(this.Class) ? MARKER_CLASS : $"{MARKER_CLASS} {this.Class}";

    /// <summary>
    /// Blocks step changes that the user triggered while the stepper is read-only.
    /// </summary>
    /// <param name="args">The interaction to inspect.</param>
    /// <returns>A completed task.</returns>
    private Task PreviewInteractionAsync(StepperInteractionEventArgs args)
    {
        if (this.ReadOnly)
            args.Cancel = true;

        return Task.CompletedTask;
    }
}