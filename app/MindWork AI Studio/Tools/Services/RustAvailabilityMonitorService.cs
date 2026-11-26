using Microsoft.AspNetCore.Components;

namespace AIStudio.Tools.Services;

/// <summary>
/// Shuts the server down once the runtime is gone.
/// </summary>
/// <remarks>
/// Services report a failed call to the runtime with the event RUST_SERVICE_UNAVAILABLE. Such a call can fail
/// while the runtime keeps running, so RuntimeOutageCheck probes the runtime before the server shuts down.
/// </remarks>
public sealed class RustAvailabilityMonitorService : BackgroundService, IMessageBusReceiver
{
    private readonly ILogger<RustAvailabilityMonitorService> logger;
    private readonly MessageBus messageBus;
    private readonly RuntimeOutageCheck outageCheck;

    public RustAvailabilityMonitorService(ILogger<RustAvailabilityMonitorService> logger, MessageBus messageBus, RustService rustService, IHostApplicationLifetime appLifetime)
    {
        this.logger = logger;
        this.messageBus = messageBus;
        this.outageCheck = new(rustService.IsRuntimeAliveAsync, appLifetime.StopApplication, RuntimeProbeSchedule.DEFAULT, appLifetime.ApplicationStopping, logger);

        this.messageBus.RegisterComponent(this);
        this.ApplyFilters([], [Event.RUST_SERVICE_UNAVAILABLE]);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        this.logger.LogInformation("The Rust availability monitor service was initialized.");
        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        this.messageBus.Unregister(this);
        await base.StopAsync(cancellationToken);
    }

    public Task ProcessMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data)
    {
        if (triggeredEvent is not Event.RUST_SERVICE_UNAVAILABLE)
            return Task.CompletedTask;

        var reason = data switch
        {
            string s when !string.IsNullOrWhiteSpace(s) => s,
            _ => "unknown reason",
        };

        //
        // The round of probes runs on its own and takes up to a minute. The service which reported
        // the failed call goes on meanwhile, e.g., the event stream with its next attempt. The check
        // watches the outcome of the round itself.
        //
        _ = this.outageCheck.Report(reason);
        return Task.CompletedTask;
    }

    public Task<MessageBusAnswer<TResult>> ProcessMessageWithResult<TPayload, TResult>(ComponentBase? sendingComponent, Event triggeredEvent, TPayload? data)
    {
        return Task.FromResult(MessageBusAnswer<TResult>.NONE);
    }
}
