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
    /// True as long as the browser of this circuit is reachable, and thus JS interop is possible.
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
    /// Called by the circuit handler when the circuit was opened.
    /// </summary>
    /// <param name="circuitId">The ID of the opened circuit.</param>
    public void AssignCircuit(string circuitId) => this.CircuitId = circuitId;

    /// <summary>
    /// Called by the circuit handler when the browser connection was established or restored.
    /// </summary>
    public void MarkAsConnected() => this.isConnected = true;

    /// <summary>
    /// Called by the circuit handler when the browser connection was lost or the circuit ended.
    /// </summary>
    public void MarkAsDisconnected() => this.isConnected = false;
}