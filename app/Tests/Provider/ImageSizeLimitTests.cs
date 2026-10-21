using AIStudio.Chat;
using AIStudio.Provider;

namespace AIStudio.Tests.Provider;

/// <summary>
/// Checks how large an image may be when we send it through a provider.
/// </summary>
/// <remarks>
/// A provider states its limit or says nothing. Either way, TryAsBase64 skips every image above our
/// own limit, so a larger limit of a provider must never reach the checks done before sending.
/// </remarks>
[TestFixture]
public sealed class ImageSizeLimitTests
{
    [Test]
    public void AnthropicLeavesRoomForTheBase64OfTheImage()
    {
        Assert.Multiple(() =>
        {
            Assert.That(LLMProviders.ANTHROPIC.GetMaxImageSizeBytes(), Is.EqualTo(3_932_160));
            Assert.That(IImageSourceExtensions.MaxImageSizeBytesFor(LLMProviders.ANTHROPIC), Is.EqualTo(3_932_160));
        });
    }

    [Test]
    public void AProviderWithoutAStatedLimitGetsOurOwn()
    {
        Assert.Multiple(() =>
        {
            Assert.That(LLMProviders.OPEN_AI.GetMaxImageSizeBytes(), Is.Null, "Nobody stated a limit, and no number stands in for that.");
            Assert.That(IImageSourceExtensions.MaxImageSizeBytesFor(LLMProviders.OPEN_AI), Is.EqualTo(IImageSourceExtensions.MAX_IMAGE_SIZE_BYTES));
        });
    }

    [Test]
    public void NoProviderGetsMoreThanTryAsBase64Sends()
    {
        Assert.Multiple(() =>
        {
            foreach (var provider in Enum.GetValues<LLMProviders>())
                Assert.That(IImageSourceExtensions.MaxImageSizeBytesFor(provider), Is.LessThanOrEqualTo(IImageSourceExtensions.MAX_IMAGE_SIZE_BYTES), provider.ToString());
        });
    }
}