using AIStudio.Provider;

namespace AIStudio.Models.Hosting.Hosts;

/// <summary>
/// Perplexity's own API.
/// </summary>
public sealed class HostPerplexity : ModelHost
{
    /// <inheritdoc />
    public override LLMProviders Provider => LLMProviders.PERPLEXITY;

    /// <inheritdoc />
    public override ModelSource Source => new("https://docs.perplexity.ai/api-reference/chat-completions-post", new DateOnly(2026, 9, 11), "Models are named plainly, and there is one API to reach them through.");
}