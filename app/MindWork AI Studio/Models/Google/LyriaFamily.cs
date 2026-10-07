using AIStudio.Provider;

using static AIStudio.Provider.Capability;

namespace AIStudio.Models.Google;

/// <summary>
/// Lyria, which writes music from a description.
/// </summary>
/// <remarks>
/// Nothing knew the name, so the catalog answered for it the way it answers for everything nobody
/// wrote a rule for: a chat model which reads text, writes text and calls tools. What comes back is
/// a stereo recording with instruments and, from 3.5 on, sung lyrics.
///
/// Nobody ran into it because the Google provider showed only names beginning with "gemini", which
/// kept four Lyria models out of sight along with the two Gemma models somebody actually wants. The
/// prefix is gone now, so the rule has to carry what the prefix carried by accident.
///
/// Whole name parts rather than a substring, for the reason Imagen gives next door. The rule is not
/// bound to Google, unlike the agent ones: wherever a model called Lyria turns up, it is this.
/// </remarks>
public sealed class LyriaFamily : ModelFamily
{
    /// <inheritdoc />
    public override ModelVendor Vendor => ModelVendor.GOOGLE;

    /// <inheritdoc />
    public override ModelSource Source => new("https://ai.google.dev/gemini-api/docs/music-generation", new DateOnly(2026, 9, 19), "A description goes in and 44.1 kHz stereo music comes out, with vocals and timed lyrics from Lyria 3.5 on. The realtime variant holds a connection open instead, which the realtime rule states and outranks this with.");

    /// <inheritdoc />
    protected override void Declare(ModelFamilyBuilder builder) =>
        builder.Rule("lyria").AsSegment()
            .Capabilities(TEXT_INPUT)
            .Kind(ModelKind.MUSIC_GENERATION);
}