using AIStudio.Provider;

namespace AIStudio.Models.Hosting.Hosts;

/// <summary>
/// The GWDG's academic cloud, which runs open weights and names them plainly.
/// </summary>
/// <remarks>
/// This is the host the transport rule was written for. The commercial models the GWDG obtains
/// from Microsoft Azure, Claude and GPT among them, are not generally available through its API:
/// its documentation lists open weights only, and its models endpoint has never answered with them.
/// An organization which gets them anyway receives them under the very names their vendors use --
/// "claude-sonnet-5", "gpt-5.5" -- so the rules recognize them and answer with everything those
/// models can do at their vendor. Everything except the API: a request goes to Göttingen, not to
/// San Francisco, and the Responses API is not served there.
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
    public override ModelSource Source => new("https://docs.hpc.gwdg.de/services/ai-services/saia/index.html", new DateOnly(2026, 10, 28), "Open weights are named plainly and served through the OpenAI-compatible API; the commercial models obtained from Azure reach the same API through a translation layer, but are not generally available for API usage.");
}