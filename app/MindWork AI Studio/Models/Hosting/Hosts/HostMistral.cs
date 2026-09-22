using AIStudio.Provider;

namespace AIStudio.Models.Hosting.Hosts;

/// <summary>
/// Mistral's own platform, which by now also serves models Mistral did not build.
/// </summary>
/// <remarks>
/// It names those under their plain names rather than prefixing them, so there is nothing to
/// unwrap here. Which model it is remains a question for the rules; what this host settles is that
/// whatever answers, it answers through Mistral's own API.
/// </remarks>
public sealed class HostMistral : ModelHost
{
    /// <inheritdoc />
    public override LLMProviders Provider => LLMProviders.MISTRAL;

    /// <inheritdoc />
    public override ModelSource Source => new("https://docs.mistral.ai/api/", new DateOnly(2026, 9, 11), "Models are named plainly, its own and the open weights it hosts alike.");
}