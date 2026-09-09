using Microsoft.AspNetCore.Components;
using Timer = System.Timers.Timer;

namespace AIStudio.Components;

/// <summary>
/// Debounced multi-line text input built on <see cref="MudTextField{T}"/>.
/// Keeps the base API while adding a debounce timer.
/// Callers can override any property as usual.
/// </summary>
public class UserPromptComponent<T> : MudTextField<T>, IDisposable
{
    [Parameter]
    public TimeSpan DebounceTime { get; set; } = TimeSpan.FromMilliseconds(800);

    [Parameter]
    public Func<string, Task> WhenTextChangedAsync { get; set; } = _ => Task.CompletedTask;

    private readonly Timer debounceTimer = new();
    private string text = string.Empty;
    private string lastParameterText = string.Empty;
    private string lastNotifiedText = string.Empty;
    private bool isInitialized;
    private bool isDisposed;

    protected override async Task OnInitializedAsync()
    {
        this.text = this.Text ?? string.Empty;
        this.lastParameterText = this.text;
        this.lastNotifiedText = this.text;
        this.debounceTimer.AutoReset = false;
        this.debounceTimer.Interval = this.DebounceTime.TotalMilliseconds;
        this.debounceTimer.Elapsed += this.WhenDebounceElapsed;

        this.isInitialized = true;
        await base.OnInitializedAsync();
    }

    protected override async Task OnParametersSetAsync()
    {
        // Ensure the timer uses the latest debouncing interval:
        if (!this.isInitialized || this.isDisposed)
        {
            await base.OnParametersSetAsync();
            return;
        }

        if(Math.Abs(this.debounceTimer.Interval - this.DebounceTime.TotalMilliseconds) > 1)
            this.debounceTimer.Interval = this.DebounceTime.TotalMilliseconds;

        // Only sync when the parent's parameter actually changed since the last change:
        if (this.Text != this.lastParameterText)
        {
            this.text = this.Text ?? string.Empty;
            this.lastParameterText = this.text;
        }

        this.debounceTimer.Stop();
        this.debounceTimer.Start();

        await base.OnParametersSetAsync();
    }

    private void WhenDebounceElapsed(object? sender, System.Timers.ElapsedEventArgs args)
    {
        this.debounceTimer.Stop();

        //
        // The timer runs on its own thread and may still fire while this component is being torn
        // down. Notifying a renderer which is already gone would throw on that thread, where no
        // caller is left to handle it.
        //
        if (this.isDisposed || this.text == this.lastNotifiedText)
            return;

        this.lastNotifiedText = this.text;
        this.InvokeAsync(async () => await this.TextChanged.InvokeAsync(this.text)).Observe($"{nameof(UserPromptComponent<T>)}: notifying about changed text");
        this.InvokeAsync(async () => await this.WhenTextChangedAsync(this.text)).Observe($"{nameof(UserPromptComponent<T>)}: handling changed text asynchronously");
    }

    #region IDisposable

    public void Dispose()
    {
        if (this.isDisposed)
            return;

        //
        // Set before stopping the timer: the handler might be running on the timer thread right
        // now, and this is what tells it to leave the gone renderer alone.
        //
        this.isDisposed = true;
        try
        {
            this.debounceTimer.Elapsed -= this.WhenDebounceElapsed;
            this.debounceTimer.Stop();
            this.debounceTimer.Dispose();
        }
        catch
        {
            // ignore
        }

        GC.SuppressFinalize(this);
    }

    #endregion
}