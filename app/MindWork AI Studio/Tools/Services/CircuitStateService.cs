namespace AIStudio.Tools.Services;

/// <summary>
/// Knows whether the browser connection of one circuit is currently up.
/// </summary>
/// <remarks>
/// There is one instance of this service per circuit, i.e. per browser window. It exists because the app
/// keeps disconnected circuits around for a long time, cf. the retention settings in Program.cs: after a
/// reload or while the machine sleeps, the components of the old circuit are still alive and still receive
/// events. They may do their work as before — only JavaScript interop is impossible while the connection
/// is gone. This is what tells them apart. Only the circuit handler changes this state.
/// </remarks>
public sealed class CircuitStateService
{
    private volatile bool isConnected = true;

    /// <summary>
    /// True, as long as the browser of this circuit is reachable, and thus JS interop is possible.
    /// </summary>
    /// <remarks>
    /// This starts as true: a circuit is created for a connected browser, and the handler reports the
    /// first connection only afterwards. Starting as false would block the interop of the first render.
    /// </remarks>
    public bool IsConnected => this.isConnected;

    /// <summary>
    /// The ID of this circuit, for logging purposes. It is "n/a" until the circuit was opened.
    /// </summary>
    public string CircuitId { get; private set; } = "n/a";

    /// <summary>
    /// Occurs when the browser connection returned after it was lost.
    /// </summary>
    /// <remarks>
    /// It does not occur for the first connection of a circuit, only for the ones which follow a loss. Use it
    /// to fetch again what the browser reports on its own: Blazor drops such reports while the connection is
    /// down, and nothing sends them a second time. The event is raised while Blazor is still completing the
    /// reconnection, though. A handler must not wait for JavaScript interop, because the browser's answer can
    /// only be processed once the reconnection has finished. Start such work without awaiting it instead.
    /// </remarks>
    public event Action? ConnectionRestored;

    /// <summary>
    /// Called by the circuit handler when the circuit was opened.
    /// </summary>
    /// <param name="circuitId">The ID of the opened circuit.</param>
    public void AssignCircuit(string circuitId) => this.CircuitId = circuitId;

    /// <summary>
    /// Called by the circuit handler when the browser connection was established or restored.
    /// </summary>
    /// <remarks>
    /// A restored connection raises ConnectionRestored. Blazor never runs the handler's events of one circuit
    /// concurrently, so reading and writing the state in two steps is safe here.
    /// </remarks>
    public void MarkAsConnected()
    {
        var wasConnected = this.isConnected;
        this.isConnected = true;
        if (!wasConnected)
            this.ConnectionRestored?.Invoke();
    }

    /// <summary>
    /// Called by the circuit handler when the browser connection was lost or the circuit ended.
    /// </summary>
    public void MarkAsDisconnected() => this.isConnected = false;
}