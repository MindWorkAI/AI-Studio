using AIStudio.Provider;

namespace AIStudio.Models.Hosting.Hosts;

/// <summary>
/// Anthropic's own cloud.
/// </summary>
public sealed class HostAnthropic : ModelHost
{
    /// <inheritdoc />
    public override LLMProviders Provider => LLMProviders.ANTHROPIC;

    /// <inheritdoc />
    public override ModelSource Source => new("https://docs.anthropic.com/en/api/messages", new DateOnly(2026, 9, 11), "Models are named plainly, and there is one API to reach them through.");
}