using AIStudio.Provider;

namespace AIStudio.Models.Hosting.Hosts;

/// <summary>
/// Hetzner's inference offering, which serves open weights under their plain names.
/// </summary>
public sealed class HostHetzner : ModelHost
{
    /// <inheritdoc />
    public override LLMProviders Provider => LLMProviders.HETZNER;

    /// <inheritdoc />
    public override ModelSource Source => new("https://experiments.hetzner.com/docs/inference", new DateOnly(2026, 9, 11), "Open weights named plainly, served through the OpenAI-compatible chat completion API.");
}