using AIStudio.Provider;

namespace AIStudio.Models.Hosting.Hosts;

/// <summary>
/// Google's own cloud.
/// </summary>
public sealed class HostGoogle : ModelHost
{
    /// <inheritdoc />
    public override LLMProviders Provider => LLMProviders.GOOGLE;

    /// <inheritdoc />
    public override ModelSource Source => new("https://ai.google.dev/gemini-api/docs/openai", new DateOnly(2026, 9, 11), "Models are named plainly, and the app reaches them through the OpenAI-compatible endpoint.");
}