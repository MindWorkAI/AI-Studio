using AIStudio.Models.Matching;
using AIStudio.Provider;

namespace AIStudio.Models.Hosting.Hosts;

/// <summary>
/// Helmholtz Blablador, which answers with the line a person would read in a menu.
/// </summary>
/// <remarks>
/// "1 - Llama3 405 the best general model" is a whole sentence, and the number in front is where
/// the entry sits in the list -- it moves whenever the operator adds a model. Taking it off is the
/// one thing this host does; the prose after the model name stays because there is no telling
/// where the name ends and the recommendation begins.
/// </remarks>
public sealed class HostHelmholtz : ModelHost
{
    /// <inheritdoc />
    public override LLMProviders Provider => LLMProviders.HELMHOLTZ;

    /// <inheritdoc />
    public override ModelSource Source => new("https://sdlaml.pages.jsc.fz-juelich.de/ai/guides/blablador_api_access/", new DateOnly(2026, 9, 11), "Models are named as menu entries, \"<position> - <description>\", and served through the OpenAI-compatible chat completion API.");

    /// <inheritdoc />
    public override bool TryUnwrap(in ModelId id, out ModelId inner, out ModelVendor? declaredVendor)
    {
        declaredVendor = null;
        return HostNaming.TryStripMenuPosition(id, out inner);
    }
}