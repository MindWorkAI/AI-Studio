using AIStudio.Tools.PluginSystem;

namespace AIStudio.Settings.DataModel;

public static class TranscriptionOpusBitrateExtensions
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(TranscriptionOpusBitrateExtensions).Namespace, nameof(TranscriptionOpusBitrateExtensions));

    public static string GetName(this TranscriptionOpusBitrate bitrate) => bitrate switch
    {
        TranscriptionOpusBitrate.KBPS_32 => TB("32 kbps (smallest upload, lowest accuracy)"),
        TranscriptionOpusBitrate.KBPS_64 => TB("64 kbps"),
        TranscriptionOpusBitrate.KBPS_128 => TB("128 kbps (recommended)"),
        TranscriptionOpusBitrate.KBPS_256 => TB("256 kbps (largest upload, highest accuracy)"),
        _ => TB("Unknown"),
    };

    public static uint GetBitsPerSecond(this TranscriptionOpusBitrate bitrate) => bitrate switch
    {
        TranscriptionOpusBitrate.KBPS_32 => 32_000,
        TranscriptionOpusBitrate.KBPS_64 => 64_000,
        TranscriptionOpusBitrate.KBPS_128 => 128_000,
        TranscriptionOpusBitrate.KBPS_256 => 256_000,

        // A value we do not know must never mean the lowest quality: that is how quiet passages
        // went missing from transcripts in the first place. Fall back to the recommended bitrate.
        _ => 128_000,
    };
}