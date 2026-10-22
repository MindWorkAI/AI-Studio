using AIStudio.Chat;
using AIStudio.Provider;
using AIStudio.Provider.SelfHosted;

namespace AIStudio.Tests.Provider;

/// <summary>
/// Checks how large an image may be when we send it through a provider.
/// </summary>
/// <remarks>
/// A provider or a self-hosted engine states its limit or says nothing. Either way, we never send
/// more than our own limit, so a larger limit of a provider must never reach the image preparation.
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
            Assert.That(IImageSourceExtensions.MaxImageSizeBytesFor(ProviderOf(LLMProviders.ANTHROPIC)), Is.EqualTo(3_932_160));
        });
    }

    [Test]
    public void AProviderWithoutAStatedLimitGetsOurOwn()
    {
        Assert.Multiple(() =>
        {
            Assert.That(LLMProviders.OPEN_AI.GetMaxImageSizeBytes(), Is.Null, "Nobody stated a limit, and no number stands in for that.");
            Assert.That(IImageSourceExtensions.MaxImageSizeBytesFor(ProviderOf(LLMProviders.OPEN_AI)), Is.EqualTo(IImageSourceExtensions.MAX_IMAGE_SIZE_BYTES));
        });
    }

    [Test]
    public void LlmmanLeavesRoomForTheTextOfTheConversation()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Host.LLMMAN.GetMaxImageSizeBytes(), Is.EqualTo(1_048_576));
            Assert.That(IImageSourceExtensions.MaxImageSizeBytesFor(ProviderOf(LLMProviders.SELF_HOSTED, Host.LLMMAN)), Is.EqualTo(1_048_576));
            Assert.That(IImageSourceExtensions.MaxImageSizeBytesFor(ProviderOf(LLMProviders.SELF_HOSTED, Host.OLLAMA)), Is.EqualTo(IImageSourceExtensions.MAX_IMAGE_SIZE_BYTES), "Ollama states no limit we know of.");
        });
    }

    [Test]
    public void AnEngineCountsOnlyForSelfHostedProviders()
    {
        // A provider keeps the host it was once configured with, even after somebody switched it
        // to a cloud provider in the dialog:
        var cloudProviderWithAnOldHost = ProviderOf(LLMProviders.OPEN_AI, Host.LLMMAN);

        Assert.That(IImageSourceExtensions.MaxImageSizeBytesFor(cloudProviderWithAnOldHost), Is.EqualTo(IImageSourceExtensions.MAX_IMAGE_SIZE_BYTES));
    }

    [Test]
    public void NoProviderGetsMoreThanOurOwnLimit()
    {
        Assert.Multiple(() =>
        {
            foreach (var llmProvider in Enum.GetValues<LLMProviders>())
                foreach (var host in Enum.GetValues<Host>())
                    Assert.That(IImageSourceExtensions.MaxImageSizeBytesFor(ProviderOf(llmProvider, host)), Is.LessThanOrEqualTo(IImageSourceExtensions.MAX_IMAGE_SIZE_BYTES), $"{llmProvider} with {host}");
        });
    }

    private static AIStudio.Settings.Provider ProviderOf(LLMProviders llmProvider, Host host = Host.NONE) => new(0, "test", "Test", llmProvider, new AIStudio.Provider.Model("model", null), Host: host);
}