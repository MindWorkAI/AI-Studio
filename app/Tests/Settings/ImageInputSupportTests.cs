using AIStudio.Provider;
using AIStudio.Settings;

namespace AIStudio.Tests.Settings;

/// <summary>
/// Checks whether a configured model counts as able to read images.
/// </summary>
/// <remarks>
/// Attaching a file to a chat, building a request, and the Batch Processing assistant all ask this one
/// question. A limit of zero images is a statement about the installation, so it has to win over the
/// capability, while a limit nobody stated must not take anything away.
/// </remarks>
[TestFixture]
public sealed class ImageInputSupportTests
{
    private const string MACHINE = "55555555-5555-5555-5555-555555555555";

    /// <summary>
    /// A model the rules know as reading text only, so that every image capability comes from the overrides.
    /// </summary>
    private static readonly Model MODEL = new("llama3.3:70b", null);

    [Test]
    public void AModelWithoutImageCapabilityCannotReadImages()
    {
        Assert.That(ProviderWith(null).SupportsImageInput(), Is.False);
    }

    [Test]
    public void AnImageCapabilityWithoutAStatedLimitReadsImages()
    {
        Assert.That(ProviderWith(new() { MultipleImageInput = true }).SupportsImageInput(), Is.True, "Nobody stated a limit, and an unknown limit takes nothing away.");
    }

    [Test]
    public void ALimitOfOneImageStillReadsImages()
    {
        Assert.That(ProviderWith(new() { MultipleImageInput = true, MaxImagesPerMessage = 1 }).SupportsImageInput(), Is.True);
    }

    [Test]
    public void ALimitOfZeroImagesPerMessageTakesTheCapabilityBack()
    {
        Assert.That(ProviderWith(new() { MultipleImageInput = true, MaxImagesPerMessage = 0 }).SupportsImageInput(), Is.False);
    }

    [Test]
    public void ALimitOfZeroImagesPerRequestTakesTheCapabilityBack()
    {
        Assert.That(ProviderWith(new() { MultipleImageInput = true, MaxImagesPerRequest = 0 }).SupportsImageInput(), Is.False, "A message cannot carry more images than its whole request.");
    }

    /// <summary>
    /// A configured self-hosted provider, the way the settings hold one.
    /// </summary>
    /// <param name="overrides">What the person switched, or nothing when they switched nothing.</param>
    /// <returns>The configured provider.</returns>
    private static AIStudio.Settings.Provider ProviderWith(ProviderCapabilityOverrides? overrides) => new(1, MACHINE, "A machine of my own", LLMProviders.SELF_HOSTED, MODEL, IsSelfHosted: true)
    {
        CapabilityOverrides = overrides,
    };
}