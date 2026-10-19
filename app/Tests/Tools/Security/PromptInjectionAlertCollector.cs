using System.Collections.Concurrent;

using AIStudio.Tools;
using AIStudio.Tools.Security;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Tests.Tools.Security;

/// <summary>
/// Collects the alerts about the sources of one test, out of whatever other tests send meanwhile.
/// </summary>
/// <param name="labelPrefix">How the labels of the sources of the test begin.</param>
internal sealed class PromptInjectionAlertCollector(string labelPrefix) : IMessageBusReceiver
{
    private readonly ConcurrentQueue<IReadOnlyList<PromptInjectionScanResult>> alerts = new();

    /// <summary>
    /// The labels of the sources in each alert, in the order the alerts arrived.
    /// </summary>
    public IReadOnlyList<IReadOnlyList<string>> Alerts => this.alerts.Select(results => results.Select(result => result.Source.Label).ToList()).ToList();

    /// <summary>
    /// Each alert as what it tells the user per source, in the order the alerts arrived.
    /// </summary>
    public IReadOnlyList<IReadOnlyList<string>> Reports => this.alerts.Select(results => results.Select(result => Describe(result.Source.Label, result.RedactedCount, result.Findings.Select(finding => finding.Snippet))).ToList()).ToList();

    public static string Describe(string label, int redactedCount, IEnumerable<string> snippets) => $"{label}: {redactedCount} passage(s), {string.Join(" | ", snippets)}";

    /// <summary>
    /// Starts collecting the alerts which the message bus delivers.
    /// </summary>
    public void Register()
    {
        MessageBus.INSTANCE.RegisterComponent(this);
        MessageBus.INSTANCE.ApplyFilters(this, [], [Event.SHOW_PROMPT_INJECTION_ALERT]);
    }

    /// <summary>
    /// Stops collecting.
    /// </summary>
    public void Unregister() => MessageBus.INSTANCE.Unregister(this);

    public Task ProcessMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data)
    {
        if (triggeredEvent is Event.SHOW_PROMPT_INJECTION_ALERT && data is PromptInjectionAlertMessage alert && alert.Results.Any(result => result.Source.Label.StartsWith(labelPrefix, StringComparison.Ordinal)))
            this.alerts.Enqueue(alert.Results);

        return Task.CompletedTask;
    }

    public Task<MessageBusAnswer<TResult>> ProcessMessageWithResult<TPayload, TResult>(ComponentBase? sendingComponent, Event triggeredEvent, TPayload? data) => Task.FromResult(MessageBusAnswer<TResult>.NONE);
}