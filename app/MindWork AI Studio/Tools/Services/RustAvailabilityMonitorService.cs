using Microsoft.AspNetCore.Components;

namespace AIStudio.Tools.Services;

public sealed class RustAvailabilityMonitorService : BackgroundService, IMessageBusReceiver
{
    private const int UNAVAILABLE_EVENT_THRESHOLD = 2;

    private readonly ILogger<RustAvailabilityMonitorService> logger;
    private readonly MessageBus messageBus;
    private readonly IHostApplicationLifetime appLifetime;

    private int rustUnavailableCount;
    
    // To prevent multiple shutdown triggers. We use int instead of bool for Interlocked operations.
    private int shutdownTriggered;

    public RustAvailabilityMonitorService(
        ILogger<RustAvailabilityMonitorService> logger,
        MessageBus messageBus,
        IHostApplicationLifetime appLifetime)
    {
        this.logger = logger;
        this.messageBus = messageBus;
        this.appLifetime = appLifetime;

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
        
        // Thread-safe incrementation of the unavailable count and check against the threshold:
        var numEvents = Interlocked.Increment(ref this.rustUnavailableCount);
        if (numEvents <= UNAVAILABLE_EVENT_THRESHOLD)
        {
            this.logger.LogWarning("Rust service unavailable (num repeats={NumRepeats}, threshold={Threshold}). Reason = '{Reason}'. Waiting for more occurrences before shutting down the server.", numEvents, UNAVAILABLE_EVENT_THRESHOLD, reason);
            return Task.CompletedTask;
        }

        // Ensure shutdown is only triggered once:
        if (Interlocked.Exchange(ref this.shutdownTriggered, 1) != 0)
            return Task.CompletedTask;

        this.logger.LogError("Rust service unavailable (num repeats={NumRepeats}, threshold={Threshold}). Reason = '{Reason}'. Shutting down the server.", numEvents, UNAVAILABLE_EVENT_THRESHOLD, reason);
        this.appLifetime.StopApplication();
        return Task.CompletedTask;
    }

    public Task<TResult?> ProcessMessageWithResult<TPayload, TResult>(ComponentBase? sendingComponent, Event triggeredEvent, TPayload? data)
    {
        return Task.FromResult<TResult?>(default);
    }
}
