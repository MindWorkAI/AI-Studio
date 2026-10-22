using AIStudio.Provider;

namespace AIStudio.Models.Hosting.Hosts;

/// <summary>
/// OpenAI's own cloud, the one place where the Responses API is actually spoken.
/// </summary>
/// <remarks>
/// This is the single host that does not put its models on the ordinary chat completion API,
/// because it is the single place the app sends a Responses API request from. Everywhere else a
/// GPT model is reached -- a gateway, a reseller, somebody's own proxy -- it is reached through the
/// ordinary API, and the host there says so.
/// </remarks>
public sealed class HostOpenAI : ModelHost
{
    /// <inheritdoc />
    public override LLMProviders Provider => LLMProviders.OPEN_AI;

    /// <inheritdoc />
    public override ModelSource Source => new("https://platform.openai.com/docs/api-reference/responses", new DateOnly(2026, 9, 11), "Models are named plainly, and both the Responses API and the chat completion API are served here.");

    /// <inheritdoc />
    /// <remarks>
    /// Nothing is taken away: whichever of the two APIs a model states, it can be reached through
    /// it here.
    /// </remarks>
    public override ModelProfile ApplyTransport(in ModelProfile profile) => profile;
}