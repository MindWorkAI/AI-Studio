using AIStudio.Provider;

using static AIStudio.Provider.Capability;

namespace AIStudio.Models.Google;

/// <summary>
/// Google's embedding models, which turn text into a vector and answer nothing.
/// </summary>
/// <remarks>
/// The previous rules answered for these with the Google default: images in, text out, tool
/// calling. None of it is true, and the app already knows better -- it asks every provider for its
/// embedding models through a method of its own.
///
/// The Gemini one needs a rule of its own for another reason: its name begins with "gemini", so
/// without one it would be read as a chat model of the family.
/// </remarks>
public sealed class GoogleEmbeddingFamily : ModelFamily
{
    /// <inheritdoc />
    public override ModelVendor Vendor => ModelVendor.GOOGLE;

    /// <inheritdoc />
    public override ModelSource Source => new("https://ai.google.dev/gemini-api/docs/embeddings", new DateOnly(2026, 9, 11), "The app lists these under IProvider.GetEmbeddingModels, which is where the statement that they embed comes from.");

    /// <inheritdoc />
    protected override void Declare(ModelFamilyBuilder builder)
    {
        builder.Rule("text-embedding-004").AsExact()
            .Capabilities(TEXT_INPUT | EMBEDDING)
            .Kind(ModelKind.EMBEDDING);

        builder.Rule("gemini-embedding").AsPrefix().Inherits();
    }
}