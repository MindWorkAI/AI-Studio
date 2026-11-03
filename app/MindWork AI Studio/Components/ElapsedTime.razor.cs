using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// Shows how long something has been running, counting up every second.
/// </summary>
/// <remarks>
/// A model which thinks for minutes shows nothing for just as long, and the time is what tells the user
/// that it still works. The component owns its timer and renders only itself once a second, not the
/// page around it: an assistant would otherwise render its entire form every second.
/// </remarks>
public partial class ElapsedTime : MSGComponentBase
{
    private static readonly TimeSpan TICK_INTERVAL = TimeSpan.FromSeconds(1);

    /// <summary>
    /// When it started.
    /// </summary>
    [Parameter]
    public DateTimeOffset Start { get; set; }

    /// <summary>Stops counting.</summary>
    private readonly CancellationTokenSource tickCancellation = new();

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        this.TickAsync(this.tickCancellation.Token).Observe($"{nameof(ElapsedTime)}: counting the elapsed time");
    }

    #endregion

    #region Overrides of MSGComponentBase

    protected override void DisposeResources()
    {
        this.tickCancellation.Cancel();
        this.tickCancellation.Dispose();
        base.DisposeResources();
    }

    #endregion

    /// <summary>
    /// Writes a time span as minutes and seconds, and with the hours in front from the first hour on.
    /// </summary>
    /// <param name="elapsed">The time span; a negative one, e.g., after the clock was set back, counts as none.</param>
    /// <returns>The time span, such as 4:07 or 1:02:09.</returns>
    internal static string Format(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero)
            elapsed = TimeSpan.Zero;

        var hours = (int)elapsed.TotalHours;
        return hours > 0
            ? $"{hours}:{elapsed.Minutes:00}:{elapsed.Seconds:00}"
            : $"{elapsed.Minutes}:{elapsed.Seconds:00}";
    }

    private async Task TickAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(TICK_INTERVAL);
        try
        {
            while (await timer.WaitForNextTickAsync(token))
                await this.InvokeAsync(this.StateHasChanged);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
    }
}