using AIStudio.Tools.Pauses;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// The two sliders of a random pause, e.g., between two indexing steps, with the shortest and the longest pause.
/// </summary>
/// <remarks>
/// The sliders keep the bounds in order, as PauseRange.GetEffective works them out: raising the
/// lower bound above the upper one raises the upper one along with it. A bound which a configuration
/// plugin locks shows a lock and keeps its value, and the other bound adjusts to it.
/// </remarks>
public partial class ConfigurationPauseRange : MSGComponentBase
{
    /// <summary>
    /// The shortest and the longest pause the feature allows.
    /// </summary>
    [Parameter, EditorRequired]
    public PauseLimits Limits { get; set; } = null!;

    /// <summary>
    /// The name of the lower bound.
    /// </summary>
    [Parameter]
    public string MinimumDescription { get; set; } = string.Empty;

    /// <summary>
    /// What the lower bound does, in more detail. Optional.
    /// </summary>
    [Parameter]
    public string MinimumHelp { get; set; } = string.Empty;

    /// <summary>
    /// The lower bound as set.
    /// </summary>
    [Parameter]
    public Func<int> Minimum { get; set; } = () => 0;

    /// <summary>
    /// Sets the lower bound.
    /// </summary>
    [Parameter]
    public Action<int> MinimumUpdate { get; set; } = _ => { };

    /// <summary>
    /// Whether a configuration plugin locks the lower bound.
    /// </summary>
    [Parameter]
    public Func<bool> IsMinimumLocked { get; set; } = () => false;

    /// <summary>
    /// The name of the upper bound.
    /// </summary>
    [Parameter]
    public string MaximumDescription { get; set; } = string.Empty;

    /// <summary>
    /// What the upper bound does, in more detail. Optional.
    /// </summary>
    [Parameter]
    public string MaximumHelp { get; set; } = string.Empty;

    /// <summary>
    /// The upper bound as set.
    /// </summary>
    [Parameter]
    public Func<int> Maximum { get; set; } = () => 0;

    /// <summary>
    /// Sets the upper bound.
    /// </summary>
    [Parameter]
    public Action<int> MaximumUpdate { get; set; } = _ => { };

    /// <summary>
    /// Whether a configuration plugin locks the upper bound.
    /// </summary>
    [Parameter]
    public Func<bool> IsMaximumLocked { get; set; } = () => false;

    /// <summary>
    /// Whether both sliders are disabled, e.g., while a run uses the bounds.
    /// </summary>
    [Parameter]
    public Func<bool> Disabled { get; set; } = () => false;

    /// <summary>
    /// Whether the bounds are settings, cf. ConfigurationSlider.IsSetting.
    /// </summary>
    [Parameter]
    public bool IsSetting { get; set; } = true;

    private PauseRange EffectiveRange => PauseRange.GetEffective(this.Limits, this.Minimum(), this.IsMinimumLocked, this.Maximum(), this.IsMaximumLocked);

    /// <remarks>
    /// A slider moves a value which falls out of its range into it. A locked slider therefore keeps
    /// the whole range, since its value belongs to the organization. A free one ends at the locked
    /// upper bound, and the free value adjusts to it.
    /// </remarks>
    private int MinimumSliderMax => this.IsMaximumLocked() && !this.IsMinimumLocked()
        ? this.EffectiveRange.MaximumSeconds
        : this.Limits.LongestSeconds;

    /// <remarks>
    /// The upper bound starts where the lower bound is, so raising the lower one above it raises the
    /// upper one along with it. A locked slider keeps the whole range, cf. MinimumSliderMax.
    /// </remarks>
    private int MaximumSliderMin => this.IsMaximumLocked()
        ? this.Limits.ShortestSeconds
        : this.EffectiveRange.MinimumSeconds;

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        this.ApplyFilters([], [ Event.CONFIGURATION_CHANGED ]);
    }

    #endregion

    #region Overrides of MSGComponentBase

    protected override Task ProcessIncomingMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data) where T : default
    {
        switch (triggeredEvent)
        {
            case Event.CONFIGURATION_CHANGED:
                this.StateHasChanged();
                break;
        }

        return Task.CompletedTask;
    }

    #endregion

    /// <remarks>
    /// Renders both sliders anew, since the range of the other one depends on this bound. A setting
    /// announces its change to all options anyway, a value which lives elsewhere does not.
    /// </remarks>
    private void UpdateMinimum(int seconds)
    {
        this.MinimumUpdate(seconds);
        this.StateHasChanged();
    }

    /// <remarks>
    /// Renders both sliders anew, cf. UpdateMinimum.
    /// </remarks>
    private void UpdateMaximum(int seconds)
    {
        this.MaximumUpdate(seconds);
        this.StateHasChanged();
    }
}