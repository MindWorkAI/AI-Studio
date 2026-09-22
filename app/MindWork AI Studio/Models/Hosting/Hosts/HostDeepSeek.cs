using AIStudio.Provider;

namespace AIStudio.Models.Hosting.Hosts;

/// <summary>
/// DeepSeek's own platform.
/// </summary>
/// <remarks>
/// It names its models by what they are for rather than by which checkpoint answers: "deepseek-chat"
/// and "deepseek-reasoner" both point at whatever is current. Those are aliases, not wrappings, so
/// there is nothing to take off -- the rules answer for the alias itself.
/// </remarks>
public sealed class HostDeepSeek : ModelHost
{
    /// <inheritdoc />
    public override LLMProviders Provider => LLMProviders.DEEP_SEEK;

    /// <inheritdoc />
    public override ModelSource Source => new("https://api-docs.deepseek.com/", new DateOnly(2026, 9, 11), "Models are named plainly, and the app reaches them through the OpenAI-compatible endpoint.");
}