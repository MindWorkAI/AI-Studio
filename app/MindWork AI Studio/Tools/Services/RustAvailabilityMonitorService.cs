using Microsoft.AspNetCore.Components;

namespace AIStudio.Tools.Services;

public sealed class RustAvailabilityMonitorService : BackgroundService, IMessageBusReceiver
{
    private const int UNAVAILABLE_EVENT_THRESHOLD = 2;

    private readonly ILogger<RustAvailabilityMonitorService> logger;
    private readonly MessageBus messageBus;
    private readonly RustService rustService;
    private readonly IHostApplicationLifetime appLifetime;

    private int rustUnavailableCount;
    private int availabilityCheckTriggered;
    
    // To prevent multiple shutdown triggers. We use int instead of bool for Interlocked operations.
    private int shutdownTriggered;

    public RustAvailabilityMonitorService(
        ILogger<RustAvailabilityMonitorService> logger,
        MessageBus messageBus,
        RustService rustService,
        IHostApplicationLifetime appLifetime)
    {
        this.logger = logger;
        this.messageBus = messageBus;
        this.rustService = rustService;
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
        
        // On the first event, trigger some Rust availability checks to confirm.
        // Just fire and forget - we don't need to await this here.
        if (numEvents == 1 && Interlocked.Exchange(ref this.availabilityCheckTriggered, 1) == 0)
        {
            //
            // This is also useful to speed up the detection of Rust availability issues,
            // as it triggers two immediate checks instead of waiting for the next scheduled check.
            // Scheduled checks are typically every few minutes, which might be too long to wait
            // in case of critical Rust service failures.
            //
            // On the other hand, we cannot kill the .NET server on the first failure, as it might
            // be a transient issue.
            //
            
            _ = this.VerifyRustAvailability();
            _ = this.VerifyRustAvailability();
        }

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

    private async Task VerifyRustAvailability()
    {
        try
        { 
            await this.rustService.ReadUserLanguage();
        }
        catch (Exception e)
        {
            this.logger.LogWarning(e, "Rust availability check failed.");
            await this.messageBus.SendMessage(null, Event.RUST_SERVICE_UNAVAILABLE, "Rust availability check failed");
        }
    }
}
