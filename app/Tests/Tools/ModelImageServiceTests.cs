using AIStudio.Chat;
using AIStudio.Provider;
using AIStudio.Tools.Rust;
using AIStudio.Tools.Services;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks when the image preparation asks the runtime again, and when it answers from its cache.
/// </summary>
/// <remarks>
/// Every message prepares all images of the conversation anew. Asking the runtime each time would
/// decode every photo again; answering from the cache after the file changed would send the old
/// picture. The runtime is played in its wire format, so these tests also fail as soon as the app
/// stops understanding what the runtime sends.
/// </remarks>
[TestFixture]
public sealed class ModelImageServiceTests
{
    private string imagePath = string.Empty;
    private FakeImageRuntime runtime = null!;
    private RustService rustService = null!;
    private ModelImageService service = null!;

    [SetUp]
    public void PrepareService()
    {
        this.imagePath = Path.Combine(Path.GetTempPath(), $"model-image-{Guid.NewGuid():N}.png");
        File.WriteAllBytes(this.imagePath, [1, 2, 3]);

        this.runtime = new FakeImageRuntime();
        this.rustService = new RustService(this.runtime);
        this.service = new ModelImageService(this.rustService, NullLogger<ModelImageService>.Instance);
    }

    [TearDown]
    public void CleanUp()
    {
        this.service.Dispose();
        this.rustService.Dispose();
        this.runtime.Dispose();
        File.Delete(this.imagePath);
    }

    [Test]
    public async Task TheSameFileForTheSameProviderIsPreparedOnce()
    {
        var first = await this.service.PrepareAsync(this.Image, ProviderOf(LLMProviders.ANTHROPIC));
        var second = await this.service.PrepareAsync(this.Image, ProviderOf(LLMProviders.ANTHROPIC));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(first.IsPrepared, Is.True);
            Assert.That(first.MimeType, Is.EqualTo("image/jpeg"));
            Assert.That(second, Is.SameAs(first));
            Assert.That(this.runtime.RequestedPaths, Has.Count.EqualTo(1), "The second message decoded the photo again.");
        }
    }

    [Test]
    public async Task AChangedFileIsPreparedAgain()
    {
        await this.service.PrepareAsync(this.Image, ProviderOf(LLMProviders.ANTHROPIC));
        await File.WriteAllBytesAsync(this.imagePath, [1, 2, 3, 4]);
        await this.service.PrepareAsync(this.Image, ProviderOf(LLMProviders.ANTHROPIC));

        Assert.That(this.runtime.RequestedPaths, Has.Count.EqualTo(2), "The model would get the picture as it was before.");
    }

    [Test]
    public async Task AnotherBudgetIsPreparedAgainWithThatBudget()
    {
        await this.service.PrepareAsync(this.Image, ProviderOf(LLMProviders.ANTHROPIC));
        await this.service.PrepareAsync(this.Image, ProviderOf(LLMProviders.OPEN_AI));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(this.runtime.RequestedBudgets, Is.EqualTo(new[] { 3_932_160, IImageSourceExtensions.MAX_IMAGE_SIZE_BYTES }));
            Assert.That(this.runtime.RequestedPaths, Is.All.EqualTo(new FileInfo(this.imagePath).FullName));
        }
    }

    [Test]
    public async Task AFailureIsTriedAgainWithTheNextMessage()
    {
        this.runtime.ErrorCode = "NOT_DECODABLE";
        var failed = await this.service.PrepareAsync(this.Image, ProviderOf(LLMProviders.ANTHROPIC));

        this.runtime.ErrorCode = "NONE";
        var prepared = await this.service.PrepareAsync(this.Image, ProviderOf(LLMProviders.ANTHROPIC));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(failed.ErrorCode, Is.EqualTo(ModelImageErrorCode.NOT_DECODABLE));
            Assert.That(failed.IsPrepared, Is.False);
            Assert.That(prepared.IsPrepared, Is.True);
            Assert.That(this.runtime.RequestedPaths, Has.Count.EqualTo(2), "A failure must not stick to the image.");
        }
    }

    [Test]
    public async Task ACodeFromANewerRuntimeNeverCountsAsAPreparedImage()
    {
        this.runtime.ErrorCode = "SOMETHING_NEW";
        var image = await this.service.PrepareAsync(this.Image, ProviderOf(LLMProviders.ANTHROPIC));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(image.ErrorCode, Is.EqualTo(ModelImageErrorCode.UNKNOWN));
            Assert.That(image.IsPrepared, Is.False, "The base64 of the answer must not go to the model when the code says nothing we know.");
        }
    }

    [Test]
    public async Task AMissingFileDoesNotReachTheRuntime()
    {
        File.Delete(this.imagePath);
        var image = await this.service.PrepareAsync(this.Image, ProviderOf(LLMProviders.ANTHROPIC));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(image.ErrorCode, Is.EqualTo(ModelImageErrorCode.FILE_NOT_FOUND));
            Assert.That(this.runtime.RequestedPaths, Is.Empty);
        }
    }

    private FileAttachmentImage Image => new("picture.png", this.imagePath, 3);

    private static AIStudio.Settings.Provider ProviderOf(LLMProviders llmProvider) => new(0, "test", "Test", llmProvider, new Model("model", null));
}