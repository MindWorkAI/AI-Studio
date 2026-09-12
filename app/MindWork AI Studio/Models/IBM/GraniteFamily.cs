using AIStudio.Provider;

using static AIStudio.Provider.Capability;

namespace AIStudio.Models.IBM;

/// <summary>
/// Granite, from IBM.
/// </summary>
/// <remarks>
/// The instruct line calls functions with the OpenAI function definition schema, so the family
/// states it and the vision checkpoints say otherwise: for those, IBM documents no tool template.
/// The thinking came in two steps -- 3.2 and 3.3 have a toggle which starts off, 4.2 thinks unless
/// the request says otherwise, and the generations in between do not think at all.
///
/// Each generation is written twice. Ollama serves them as "granite4.2:8b", with the version glued
/// to the family name, while IBM writes "granite-4.2". The previous rules knew only IBM's spelling,
/// so everything anybody actually ran through Ollama quietly lost its thinking.
/// </remarks>
public sealed class GraniteFamily : ModelFamily
{
    /// <inheritdoc />
    public override ModelVendor Vendor => ModelVendor.IBM;

    /// <inheritdoc />
    public override ModelSource Source => new("https://www.ibm.com/granite/docs/models/granite/", new DateOnly(2026, 9, 11), "Ported from the Granite block of ProviderExtensions.OpenSource.cs, with the spelling Ollama uses added to each generation.");

    /// <inheritdoc />
    protected override void Declare(ModelFamilyBuilder builder)
    {
        builder.Rule("granite").AsSubstring()
            .Capabilities(TEXT_INPUT | TEXT_OUTPUT | FUNCTION_CALLING)
            .Apis(CHAT_COMPLETION_API);

        // The embedding checkpoints turn text into a vector; there is no conversation in them:
        builder.Rule("granite-embedding").AsSubstring()
            .Capabilities(TEXT_INPUT | EMBEDDING)
            .Kind(ModelKind.EMBEDDING);

        // The vision checkpoints look at pictures and have nothing to call a function with:
        builder.Rule("granite").AsSubstring().AlsoContains("vision")
            .Capabilities(TEXT_INPUT | MULTIPLE_IMAGE_INPUT | TEXT_OUTPUT)
            .Apis(CHAT_COMPLETION_API);

        // From 4.2 on they think unless the request says otherwise:
        builder.Rule("granite-4.2").AsSubstring().NotContains("vision")
            .Capabilities(TEXT_INPUT | TEXT_OUTPUT | FUNCTION_CALLING)
            .Apis(CHAT_COMPLETION_API)
            .Reasoning(ReasoningSupport.ON_BY_DEFAULT);

        builder.Rule("granite4.2").AsSubstring().NotContains("vision").Inherits();

        // 3.2 and 3.3 have to be asked:
        builder.Rule("granite-3.2").AsSubstring().NotContains("vision")
            .Capabilities(TEXT_INPUT | TEXT_OUTPUT | FUNCTION_CALLING)
            .Apis(CHAT_COMPLETION_API)
            .Reasoning(ReasoningSupport.OPTIONAL);

        builder.Rule("granite3.2").AsSubstring().NotContains("vision").Inherits();

        builder.Rule("granite-3.3").AsSubstring().NotContains("vision").Inherits();

        builder.Rule("granite3.3").AsSubstring().NotContains("vision").Inherits();
    }
}