using AIStudio.Provider;

namespace AIStudio.Models.Hosting.Hosts;

/// <summary>
/// xAI's own API, where Grok comes from.
/// </summary>
public sealed class HostX : ModelHost
{
    /// <inheritdoc />
    public override LLMProviders Provider => LLMProviders.X;

    /// <inheritdoc />
    public override ModelSource Source => new("https://docs.x.ai/docs/api-reference", new DateOnly(2026, 9, 11), "Models are named plainly, and the app reaches them through the OpenAI-compatible endpoint.");
}