using AIStudio.Models.Registry;
using AIStudio.Provider;

namespace AIStudio.Tests.Models.ZAI;

/// <summary>
/// Checks how a GLM name says that the model looks at pictures.
/// </summary>
/// <remarks>
/// Z AI marks its vision models by gluing a "v" to the version number: glm-4v, glm-4.1v, glm-4.5v.
/// That is not a name part, so no pattern can ask about it and the family works it out of the name
/// instead -- the second of the two places in the rebuilt rules where a capability is calculated.
///
/// These need tests of their own because the corpus cannot tell the calculation apart from a
/// careless one. Looking for a bare "v" anywhere answers every corpus name the same way, and is
/// still wrong: a quantized build carries one in "nvfp4", and so do the names of several inference
/// providers. The corpus happens to hold that name only for a generation which reads images anyway.
/// </remarks>
[TestFixture]
public sealed class GlmFamilyTests
{
    [TestCase("glm-4.5v", TestName = "The vision marker sits behind the version")]
    [TestCase("glm-4v", TestName = "A version without a dot carries the marker just the same")]
    [TestCase("glm-4.1v-9b", TestName = "A size may follow the marker")]
    public void AGlmWhoseVersionCarriesTheMarkerLooksAtPictures(string modelId)
    {
        var profile = ModelRegistry.Shared.Profile(LLMProviders.SELF_HOSTED, modelId);

        Assert.That(profile.Has(Capability.MULTIPLE_IMAGE_INPUT), Is.True);
    }

    [TestCase("glm-4-9b-chat-nvfp4", TestName = "A quantized build is not a vision model")]
    [TestCase("glm-4-9b-chat", TestName = "The plain 4 line reads text only")]
    [TestCase("glm-4.6-latest", TestName = "A rolling tag says nothing about pictures")]
    public void AGlmCarryingAVSomewhereElseDoesNot(string modelId)
    {
        var profile = ModelRegistry.Shared.Profile(LLMProviders.SELF_HOSTED, modelId);

        Assert.That(profile.Has(Capability.MULTIPLE_IMAGE_INPUT), Is.False);
    }
}