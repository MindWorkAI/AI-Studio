namespace AIStudio.Tools.Services;

/// <summary>
/// Coordinates UI recovery after the Blazor circuit reconnects.
/// </summary>
public sealed class ReconnectRecoveryService
{
    private int generation;

    /// <summary>
    /// Raised when the reconnect recovery generation changes.
    /// </summary>
    public event Action? Changed;

    /// <summary>
    /// Gets the current reconnect recovery generation.
    /// </summary>
    public int Generation => Volatile.Read(ref this.generation);

    /// <summary>
    /// Marks reconnect recovery as completed and requests a route subtree remount.
    /// </summary>
    public void NotifyRecovered()
    {
        Interlocked.Increment(ref this.generation);
        this.Changed?.Invoke();
    }
}
