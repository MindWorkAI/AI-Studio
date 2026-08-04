using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// Shows a lock icon for features that a configuration plugin has disabled. The related
/// control stays visible but must be disabled by the caller, so users still see that the
/// feature exists and learn that their organization has locked it.
/// </summary>
public partial class ManagedFeatureLock : MSGComponentBase
{
    /// <summary>
    /// Is the feature locked by a configuration plugin?
    /// </summary>
    [Parameter]
    public Func<bool> IsLocked { get; set; } = () => false;

    /// <summary>
    /// An optional text that explains the lock. Without it, the generic explanation is used.
    /// </summary>
    [Parameter]
    public string LockText { get; set; } = string.Empty;

    /// <summary>
    /// Where should the tooltip be placed?
    /// </summary>
    [Parameter]
    public Placement Placement { get; set; } = Placement.Left;

    /// <summary>
    /// The CSS class to apply to the lock icon.
    /// </summary>
    [Parameter]
    public string Class { get; set; } = "mr-1";

    private string LockTextOrDefault => string.IsNullOrWhiteSpace(this.LockText)
        ? this.T("This feature is managed by your organization and has therefore been disabled.")
        : this.LockText;

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        this.ApplyFilters([], [ Event.CONFIGURATION_CHANGED ]);
        await base.OnInitializedAsync();
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
}