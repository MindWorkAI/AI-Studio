using AIStudio.Tools.Pauses;

using Microsoft.AspNetCore.Components;

using Timer = System.Timers.Timer;

namespace AIStudio.Components;

/// <summary>
/// One slider with two handles for a random pause, e.g., between two indexing steps: the shortest and the longest pause.
/// </summary>
/// <remarks>
/// The slider shows the bounds which apply, as PauseRange.GetEffective works them out, and stores
/// them that way: pushing the lower handle past the upper one takes the upper one along. A bound
/// which a configuration plugin locks keeps its value, its handle cannot move, and the option shows
/// a lock. The handles move in steps of five seconds; a bound off these steps, e.g., one which a
/// configuration plugin sets, applies as it is, and only its handle shows the nearest step.
/// </remarks>
public partial class ConfigurationPauseRange : ConfigurationBaseCore
{
    /// <summary>
    /// The seconds between two tick marks, and the step of the handles.
    /// </summary>
    private const int STEP_SECONDS = 5;

    /// <summary>
    /// Initializes the component, which shows the lock of the option while either bound is locked.
    /// </summary>
    public ConfigurationPauseRange()
    {
        this.IsLocked = () => this.IsMinimumLocked() || this.IsMaximumLocked();
    }

    /// <summary>
    /// The shortest and the longest pause the feature allows.
    /// </summary>
    [Parameter, EditorRequired]
    public PauseLimits Limits { get; set; } = null!;

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
    /// Whether the bounds are settings, which a change stores and announces to the other options.
    /// </summary>
    /// <remarks>
    /// Defaults to true. False for bounds which live elsewhere, e.g., in the session of an assistant.
    /// </remarks>
    [Parameter]
    public bool IsSetting { get; set; } = true;

    /// <summary>
    /// Stores the settings once the user stopped moving a handle: storing them with every step
    /// would make the slider stutter, since the store runs a while.
    /// </summary>
    private readonly Timer storeTimer = new(TimeSpan.FromMilliseconds(500))
    {
        AutoReset = false
    };

    /// <summary>
    /// Whether a change waits for storeTimer.
    /// </summary>
    private bool isStorePending;

    private PauseRange EffectiveRange => PauseRange.GetEffective(this.Limits, this.Minimum(), this.IsMinimumLocked, this.Maximum(), this.IsMaximumLocked);

    /// <summary>
    /// The start of the track: the shortest pause, or the step right below it.
    /// </summary>
    private int TrackMin => this.Limits.ShortestSeconds / STEP_SECONDS * STEP_SECONDS;

    /// <summary>
    /// The end of the track: the longest pause, or the step right above it.
    /// </summary>
    private int TrackMax => (this.Limits.LongestSeconds + STEP_SECONDS - 1) / STEP_SECONDS * STEP_SECONDS;

    #region Overrides of ConfigurationBase

    /// <inheritdoc />
    protected override bool Stretch => true;

    /// <inheritdoc />
    protected override Variant Variant => Variant.Outlined;

    protected override string Label => this.OptionDescription;

    #endregion

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        // Off the renderer, which stays free while the settings are stored:
        this.storeTimer.Elapsed += (_, _) => this.StoreAsync().Observe($"{nameof(ConfigurationPauseRange)}: storing the changed pause");
        await base.OnInitializedAsync();
    }

    #endregion

    #region Overrides of MSGComponentBase

    protected override void DisposeResources()
    {
        this.storeTimer.Stop();
        this.storeTimer.Dispose();

        // A change made right before closing still reaches the disk:
        if (this.isStorePending)
            this.StoreAsync().Observe($"{nameof(ConfigurationPauseRange)}: storing the changed pause while closing");

        base.DisposeResources();
    }

    #endregion

    private string GetRangeText(PauseRange range)
    {
        if (range.IsNone)
            return T("No pause");

        return range.MinimumSeconds == range.MaximumSeconds
            ? string.Format(T("{0} seconds"), range.MinimumSeconds)
            : string.Format(T("{0} to {1} seconds"), range.MinimumSeconds, range.MaximumSeconds);
    }

    private void UpdateMinimum(int seconds) => this.ApplyBounds(seconds, this.Maximum());

    private void UpdateMaximum(int seconds) => this.ApplyBounds(this.Minimum(), seconds);

    /// <summary>
    /// Sets both bounds as they apply, so that the handles show what is stored.
    /// </summary>
    /// <remarks>
    /// A locked bound keeps its value, even when the slider reports another one, e.g., while it
    /// puts its handles in order.
    /// </remarks>
    private void ApplyBounds(int minimumSeconds, int maximumSeconds)
    {
        var range = PauseRange.GetEffective(this.Limits, minimumSeconds, this.IsMinimumLocked, maximumSeconds, this.IsMaximumLocked);
        if (!this.IsMinimumLocked())
            this.MinimumUpdate(range.MinimumSeconds);

        if (!this.IsMaximumLocked())
            this.MaximumUpdate(range.MaximumSeconds);

        if (!this.IsSetting)
            return;

        this.isStorePending = true;
        this.storeTimer.Stop();
        this.storeTimer.Start();
    }

    private async Task StoreAsync()
    {
        this.isStorePending = false;
        await this.SettingsManager.StoreSettings();
        await this.InformAboutChange();
    }
}