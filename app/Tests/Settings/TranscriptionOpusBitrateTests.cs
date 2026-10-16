using AIStudio.Settings.DataModel;

namespace AIStudio.Tests.Settings;

/// <summary>
/// Checks which bitrate the transcription pipeline ends up asking the Opus encoder for.
/// </summary>
/// <remarks>
/// This number is the whole reason the setting exists. Until v26.9.1 the encoder was fixed at
/// 32 kbps, and transcription models silently dropped what had been spoken quietly -- a greeting at
/// the start of a recording never reached the transcript. Two ways back to that value have to stay
/// closed: the arm catching a bitrate nobody declared, and the member a settings file the app cannot
/// read falls back to. Both are one edit away from pointing at the lowest quality again.
/// </remarks>
[TestFixture]
public sealed class TranscriptionOpusBitrateTests
{
    private static readonly Dictionary<TranscriptionOpusBitrate, uint> EXPECTED_BITS_PER_SECOND = new()
    {
        [TranscriptionOpusBitrate.KBPS_32] = 32_000,
        [TranscriptionOpusBitrate.KBPS_64] = 64_000,
        [TranscriptionOpusBitrate.KBPS_128] = 128_000,
        [TranscriptionOpusBitrate.KBPS_256] = 256_000,
    };

    [Test]
    public void EveryOfferedBitrateAsksForWhatItsNameSays()
    {
        foreach (var bitrate in Enum.GetValues<TranscriptionOpusBitrate>())
        {
            Assert.That(EXPECTED_BITS_PER_SECOND.ContainsKey(bitrate), Is.True, $"The selection offers {bitrate}, so this test has to state what that is worth in bits per second.");
            Assert.That(bitrate.GetBitsPerSecond(), Is.EqualTo(EXPECTED_BITS_PER_SECOND[bitrate]), $"{bitrate} is what the user picked; anything else travels to the encoder behind their back.");
        }
    }

    [Test]
    public void ABitrateNobodyDeclaredFallsBackToTheRecommendedOne()
    {
        var undeclared = (TranscriptionOpusBitrate)999;

        Assert.That(Enum.IsDefined(undeclared), Is.False, "The point of this test is a value outside the enum; a declared one would prove nothing.");
        Assert.That(undeclared.GetBitsPerSecond(), Is.EqualTo(128_000u), "Not knowing which quality was meant is no reason to pick the worst one available.");
    }

    [Test]
    public void AnUnreadableSettingsValueLandsOnTheRecommendedBitrate()
    {
        //
        // TolerantEnumConverter answers a value it cannot parse with the member whose underlying
        // value is zero. Which member that is decides what a damaged settings file transcribes with:
        //
        Assert.That(default(TranscriptionOpusBitrate), Is.EqualTo(TranscriptionOpusBitrate.KBPS_128), "The member with the underlying value zero is what a settings file the app cannot read falls back to, so it has to be the recommended bitrate.");
    }
}