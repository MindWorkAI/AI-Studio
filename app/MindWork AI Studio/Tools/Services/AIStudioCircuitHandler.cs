using Microsoft.AspNetCore.Components.Server.Circuits;

namespace AIStudio.Tools.Services;

/// <summary>
/// Follows the life of one circuit, so the rest of the app knows when its browser is unreachable.
/// </summary>
/// <remarks>
/// The app keeps disconnected circuits for a long time on purpose, cf. the retention settings in
/// Program.cs. That is what lets a user return to a working app after the machine woke up — but it also
/// means that the components of reloaded or sleeping windows stay alive and keep receiving events. They
/// may keep working: everything they do on the server is fine. Only JavaScript interop is impossible
/// while the connection is gone. So this handler does two things, and deliberately nothing more:
/// it publishes the connection state, and it cleans up once a circuit is truly over.
/// </remarks>
public sealed class AIStudioCircuitHandler(CircuitStateService circuitState, MessageBus messageBus, ILogger<AIStudioCircuitHandler> logger)
    : CircuitHandler
{
    #region Overrides of CircuitHandler

    public override Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        circuitState.AssignCircuit(circuit.Id);
        logger.LogInformation("The circuit '{CircuitId}' was opened.", circuit.Id);

        return Task.CompletedTask;
    }

    public override Task OnConnectionUpAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        circuitState.MarkAsConnected();
        logger.LogInformation("The browser connection of the circuit '{CircuitId}' is up.", circuit.Id);

        return Task.CompletedTask;
    }

    public override Task OnConnectionDownAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        circuitState.MarkAsDisconnected();
        logger.LogInformation("The browser connection of the circuit '{CircuitId}' is down. Its JavaScript interop is paused until it returns.", circuit.Id);

        return Task.CompletedTask;
    }

    public override Task OnCircuitClosedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        circuitState.MarkAsDisconnected();

        //
        // The components of this circuit will not come back, so nobody would ever deregister them:
        // Blazor disposes components of a retained circuit without giving them a chance to run their
        // disposal in every case. Without this, the message bus would keep and serve them forever.
        //
        var numRemovedReceivers = messageBus.UnregisterCircuit(circuitState);
        logger.LogInformation("The circuit '{CircuitId}' was closed. Removed {NumReceivers} message bus receiver(s) of that circuit.", circuit.Id, numRemovedReceivers);

        return Task.CompletedTask;
    }

    #endregion
}