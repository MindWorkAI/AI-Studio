using AIStudio.Provider;

namespace AIStudio.Models.Hosting.Hosts;

/// <summary>
/// The GWDG's academic cloud, which resells commercial models next to the open weights it runs.
/// </summary>
/// <remarks>
/// This is the host the transport rule was written for. It offers Claude and GPT under the very
/// names their vendors use -- "claude-sonnet-5", "gpt-5.5" -- so the rules recognize them and
/// answer with everything those models can do at their vendor. Everything except the API: a request
/// goes to Göttingen, not to San Francisco, and the Responses API is not served there.
///
/// The old code arrived at the same answer by having the open weights rules notice a Claude name
/// and call the Anthropic rules, then correct the result. Here the recognizing and the correcting
/// are two different things in two different places, which is why neither has to know about the
/// other.
/// </remarks>
public sealed class HostGWDG : ModelHost
{
    /// <inheritdoc />
    public override LLMProviders Provider => LLMProviders.GWDG;

    /// <inheritdoc />
    public override ModelSource Source => new("https://docs.hpc.gwdg.de/services/saia/index.html", new DateOnly(2026, 9, 11), "Open weights and resold commercial models alike are named plainly, and all of them are served through the OpenAI-compatible chat completion API.");
}