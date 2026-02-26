using Microsoft.AspNetCore.Components;
using Timer = System.Timers.Timer;
using MudBlazor;

namespace AIStudio.Components;

/// <summary>
/// Debounced multi-line text input built on <see cref="MudTextField{T}"/>.
/// Keeps the base API while adding a debounce timer.
/// Callers can override any property as usual.
/// </summary>
public class UserPromptComponent<T> : MudTextField<T>
{
    [Parameter]
    public TimeSpan DebounceTime { get; set; } = TimeSpan.FromMilliseconds(800);

    // Use base Text / TextChanged from MudTextField; do not redeclare to avoid duplicate parameters.
    // Text binding is handled through those base members; we only add debouncing behavior.

    [Parameter]
    public Func<string, Task> WhenTextChangedAsync { get; set; } = _ => Task.CompletedTask;
    
    private readonly Timer debounceTimer = new();
    private string text = string.Empty;
    private string lastParameterText = string.Empty;
    private string lastNotifiedText = string.Empty;
    private bool isInitialized;
    
    protected override async Task OnInitializedAsync()
    {
        this.text = this.Text ?? string.Empty;
        this.lastParameterText = this.Text ?? string.Empty;
        this.lastNotifiedText = this.Text ?? string.Empty;
        this.debounceTimer.AutoReset = false;
        this.debounceTimer.Interval = this.DebounceTime.TotalMilliseconds;
        this.debounceTimer.Elapsed += (_, _) =>
        {
            this.debounceTimer.Stop();
            if (this.text == this.lastNotifiedText)
                return;
            
            this.lastNotifiedText = this.text;
            this.InvokeAsync(async () => await this.TextChanged.InvokeAsync(this.text));
            this.InvokeAsync(async () => await this.WhenTextChangedAsync(this.text));
        };
        
        this.isInitialized = true;
        await base.OnInitializedAsync();
    }
    
    protected override async Task OnParametersSetAsync()
    {
        // Ensure the timer uses the latest debouncing interval:
        if (!this.isInitialized)
            return;
        
        if(Math.Abs(this.debounceTimer.Interval - this.DebounceTime.TotalMilliseconds) > 1)
            this.debounceTimer.Interval = this.DebounceTime.TotalMilliseconds;
        
        // Only sync when the parent's parameter actually changed since the last change:
        if (this.Text != this.lastParameterText)
        {
            this.text = this.Text ?? string.Empty;
            this.lastParameterText = this.Text ?? string.Empty;
        }
        
        this.debounceTimer.Stop();
        this.debounceTimer.Start();
        
        await base.OnParametersSetAsync();
    }
}
