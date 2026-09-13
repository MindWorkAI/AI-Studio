using AIStudio.Models;
using AIStudio.Provider;

namespace AIStudio.Tests.Models;

/// <summary>
/// Checks the two things about the capability enum which the rest of the app relies on.
/// </summary>
/// <remarks>
/// Capabilities became a set carried in one value, which only works while each member owns a bit of
/// its own. And the names are what an override written years ago addresses, so a member which is no
/// longer handed out still has to answer to its name.
/// </remarks>
[TestFixture]
public sealed class CapabilityTests
{
    /// <summary>
    /// Every capability the app has ever written into a configuration.
    /// </summary>
    /// <remarks>
    /// Deliberately spelled out instead of read from the enum: a test which asks the enum about
    /// itself would agree with any change made to it, including a member being deleted. Removing
    /// one of these names silently drops the override an organization wrote for it.
    /// </remarks>
    private static readonly string[] NAMES_THAT_MUST_KEEP_WORKING =
    [
        "NONE", "UNKNOWN",
        "TEXT_INPUT", "AUDIO_INPUT", "SINGLE_IMAGE_INPUT", "MULTIPLE_IMAGE_INPUT", "SPEECH_INPUT", "VIDEO_INPUT",
        "TEXT_OUTPUT", "AUDIO_OUTPUT", "IMAGE_OUTPUT", "SPEECH_OUTPUT", "VIDEO_OUTPUT",
        "OPTIONAL_REASONING", "ALWAYS_REASONING", "REASONING_BY_DEFAULT",
        "EMBEDDING", "REALTIME", "FUNCTION_CALLING", "WEB_SEARCH",
        "CHAT_COMPLETION_API", "RESPONSES_API",
    ];

    [Test]
    public void EveryCapabilityOwnsOneBitOfItsOwn()
    {
        var bits = new Dictionary<ulong, Capability>();

        Assert.Multiple(() =>
        {
            foreach (var capability in Enum.GetValues<Capability>())
            {
                if (capability is Capability.NONE)
                    continue;

                var value = (ulong) capability;
                Assert.That(ulong.IsPow2(value), Is.True, $"{capability} is not a single bit, so it cannot be part of a set.");

                if (bits.TryGetValue(value, out var other))
                    Assert.Fail($"{capability} and {other} share a bit, so the app cannot tell them apart.");

                bits[value] = capability;
            }
        });
    }

    [Test]
    public void NoCapabilityLostItsName() => Assert.That(Enum.GetNames<Capability>(), Is.SupersetOf(NAMES_THAT_MUST_KEEP_WORKING));

    [Test]
    public void TheReasoningVocabularyIsExactlyTheThreeReasoningMembers()
    {
        const Capability THE_THREE = Capability.OPTIONAL_REASONING | Capability.ALWAYS_REASONING | Capability.REASONING_BY_DEFAULT;

        Assert.That(ModelProfile.REASONING_VOCABULARY, Is.EqualTo(THE_THREE));
    }
}