using AIStudio.Provider;

namespace AIStudio.Models.Kinds;

/// <summary>
/// The models which hold a spoken conversation over a live connection.
/// </summary>
/// <remarks>
/// They speak a protocol of their own, usually a WebSocket, and answer a chat completion request
/// with an error. Their names are built out of the models they grew from -- gpt-4o-realtime-preview,
/// gpt-realtime-mini -- so a name of this kind regularly carries a word about hearing or speaking as
/// well. Whichever of the two is longer would otherwise decide, and the live connection is the part
/// that makes the model unusable for a chat.
/// </remarks>
public sealed class RealtimeModelsFamily : ModelFamily
{
    /// <summary>
    /// Why this outranks what a name says about hearing or speaking.
    /// </summary>
    private const string THE_CONNECTION_DECIDES = "These names are built from the transcription and audio models they grew out of, so those markers match them too. The live connection is what rules out a chat, no matter what else the name says.";

    /// <inheritdoc />
    public override ModelVendor Vendor => ModelVendor.UNKNOWN;

    /// <inheritdoc />
    public override ModelSource Source => new("https://developers.openai.com/api/docs/models/gpt-live-1", new DateOnly(2026, 9, 12), "Ported from the realtime marker of Provider/ModelKindExtensions.cs, where the same precedence was written as the order of two if statements. The live rule was added after GPT-Live turned up in the chat list while testing, and widened when Google's catalog turned out to name its whole two-way line that way: https://ai.google.dev/gemini-api/docs/live");

    /// <inheritdoc />
    protected override void Declare(ModelFamilyBuilder builder)
    {
        builder.Modifier("realtime").AsSubstring()
            .Rank(1, THE_CONNECTION_DECIDES)
            .Kind(ModelKind.REALTIME);

        //
        // The other word for the same connection. OpenAI's GPT-Live listens and speaks at once and
        // leaves the thinking to a text model behind it, and Google names its whole two-way line
        // this way: gemini-3.8-live, gemini-3.1-flash-live-preview, gemini-3.5-live-translate-preview.
        // None of them can be talked to the way a chat model can, and all of them stood in the chat
        // list until this rule was written.
        //
        // A name part rather than a substring, because four letters sit inside "delivery",
        // "olive" and plenty of words which promise no connection at all.
        //
        builder.Modifier("live").AsSegment().Inherits();
    }
}