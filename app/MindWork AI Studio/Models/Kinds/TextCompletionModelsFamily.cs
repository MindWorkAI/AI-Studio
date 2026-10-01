using AIStudio.Provider;

namespace AIStudio.Models.Kinds;

/// <summary>
/// The models from before chat completions existed.
/// </summary>
/// <remarks>
/// Providers keep offering some of them -- Helmholtz Blablador still reports text-davinci-003 --
/// but asking any of them for a chat completion fails. They only answer through the completions
/// endpoint, which the app does not speak, so they must not stand among the chat models.
///
/// "ada" is deliberately not among these names: three letters appear in far too many unrelated ones,
/// and losing a chat model weighs heavier than keeping a dead one in the list.
/// </remarks>
public sealed class TextCompletionModelsFamily : ModelFamily
{
    /// <inheritdoc />
    public override ModelVendor Vendor => ModelVendor.UNKNOWN;

    /// <inheritdoc />
    public override ModelSource Source => new("https://platform.openai.com/docs/api-reference/completions", new DateOnly(2026, 9, 12), "Ported unchanged from the text completion markers of Provider/ModelKindExtensions.cs. Fill-in-the-middle came later, off Mistral's own catalog at https://api.mistral.ai/v1/models, read on 2026-09-19.");

    /// <inheritdoc />
    protected override void Declare(ModelFamilyBuilder builder)
    {
        builder.Modifier("davinci").AsSubstring().Kind(ModelKind.TEXT_COMPLETION);

        builder.Modifier("babbage").AsSubstring().Inherits();

        builder.Modifier("curie").AsSubstring().Inherits();

        // The one model of the 3.5 line which never learned to chat, next to the ones which did:
        builder.Modifier("gpt-3.5-turbo-instruct").AsSegment().Inherits();

        //
        // Fill in the middle: a model handed the code on either side of a gap rather than a
        // conversation. Mistral writes it into the name of the one it serves for that,
        // mistral-code-fim-latest, which stood in the chat list because nothing looked for the
        // word. Codestral does the same job and says so through its family instead.
        //
        builder.Modifier("fim").AsSegment().Inherits();
    }
}