using AIStudio.Assistants.BeyondFactsLab;
using AIStudio.Chat;
using AIStudio.Settings;
using AIStudio.Tests.Tools;
using AIStudio.Tools;
using AIStudio.Tools.Security;
using AIStudio.Tools.Services;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.BeyondFactsLab;

/// <summary>
/// Checks how the BeyondFacts Lab adds an image to the material of a preparation: as a copy which the runtime
/// prepared for models, never as a path to the file of the person.
/// </summary>
/// <remarks>
/// FakeImageRuntime plays the runtime and hands back the same three bytes for every image.
/// </remarks>
[TestFixture]
public sealed class BflImageMaterialTests
{
    private string dataDirectory = string.Empty;
    private FakeImageRuntime runtime = null!;
    private RustService rustService = null!;
    private MediaTranscriptionService transcriptionService = null!;
    private BeyondFactsLabStore store = null!;
    private BflMaterialService service = null!;

    [SetUp]
    public void PrepareService()
    {
        this.dataDirectory = Path.Combine(Path.GetTempPath(), $"ai-studio-beyondfacts-lab-images-{Guid.NewGuid():N}");
        Directory.CreateDirectory(this.dataDirectory);

        this.runtime = new FakeImageRuntime();
        this.rustService = new RustService(this.runtime);
        var settingsManager = new SettingsManager(NullLogger<SettingsManager>.Instance, this.rustService);
        var guardService = new PromptInjectionGuardService(this.rustService, settingsManager, NullLogger<PromptInjectionGuardService>.Instance, NullLoggerFactory.Instance);
        this.transcriptionService = new MediaTranscriptionService(null!, settingsManager, null!, [], NullLogger<MediaTranscriptionService>.Instance);
        this.store = new BeyondFactsLabStore(NullLogger<BeyondFactsLabStore>.Instance, new BflStorageOptions { DataDirectory = this.dataDirectory });
        this.service = new BflMaterialService(this.store, this.rustService, guardService, MessageBus.INSTANCE, settingsManager, this.transcriptionService, NullLogger<BflMaterialService>.Instance);
    }

    [TearDown]
    public void CleanUp()
    {
        this.transcriptionService.Dispose();
        this.rustService.Dispose();
        this.runtime.Dispose();
        if (Directory.Exists(this.dataDirectory))
            Directory.Delete(this.dataDirectory, true);
    }

    [TestCase("image/jpeg", "jpg")]
    [TestCase("image/png", "png")]
    public async Task AnImageBecomesACopyPreparedForModels(string mimeType, string extension)
    {
        this.runtime.MimeType = mimeType;
        var project = await this.store.CreateAsync("Panel");
        var poster = await this.WriteFileAsync("Poster.tiff");

        await this.service.ImportAsync(project.ProjectId, [poster], null!);
        var material = (await this.store.ListMaterialsAsync(project.ProjectId)).Single().Material!;
        var copy = this.store.MaterialImagePath(project.ProjectId, material);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(material.Kind, Is.EqualTo(BflMaterialKind.IMAGE));
            Assert.That(material.Name, Is.EqualTo("Poster.tiff"));
            Assert.That(material.Image!.Extension, Is.EqualTo(extension), "The runtime tells the format of the copy by its extension.");
            Assert.That(await File.ReadAllBytesAsync(copy), Is.EqualTo(Convert.FromBase64String(FakeImageRuntime.IMAGE_BASE64)));
            Assert.That(copy, Does.StartWith(Path.Combine(this.dataDirectory, BeyondFactsLabStore.ROOT_DIRECTORY_NAME)), "The material must not depend on the file of the person.");
            Assert.That(this.runtime.RequestedPaths, Is.EqualTo(new[] { poster }));
            Assert.That(this.runtime.RequestedBudgets, Is.EqualTo(new[] { IImageSourceExtensions.MAX_IMAGE_SIZE_BYTES }), "The copy must be as large as any provider takes.");
        }
    }

    [Test]
    public async Task AnImageTheRuntimeCannotPrepareIsLeftOut()
    {
        this.runtime.ErrorCode = "NOT_DECODABLE";
        var project = await this.store.CreateAsync("Panel");

        await this.service.ImportAsync(project.ProjectId, [await this.WriteFileAsync("Poster.png")], null!);

        Assert.That(await this.store.ListMaterialsAsync(project.ProjectId), Is.Empty);
    }

    [Test]
    public async Task TheSameImageIsAddedOnce()
    {
        var project = await this.store.CreateAsync("Panel");
        var poster = await this.WriteFileAsync("Poster.png");

        await this.service.ImportAsync(project.ProjectId, [poster], null!);
        await this.service.ImportAsync(project.ProjectId, [poster], null!);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await this.store.ListMaterialsAsync(project.ProjectId), Has.Count.EqualTo(1));
            Assert.That(this.runtime.RequestedPaths, Has.Count.EqualTo(1), "The lab prepared an image the preparation had already.");
        }
    }

    [Test]
    public async Task AnImageModelsCannotTakeIsNoMaterial()
    {
        var project = await this.store.CreateAsync("Panel");

        await this.service.ImportAsync(project.ProjectId, [await this.WriteFileAsync("Poster.heic")], null!);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await this.store.ListMaterialsAsync(project.ProjectId), Is.Empty);
            Assert.That(this.runtime.RequestedPaths, Is.Empty);
        }
    }

    private async Task<string> WriteFileAsync(string name)
    {
        var folder = Path.Combine(this.dataDirectory, "person", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, name);
        await File.WriteAllBytesAsync(path, [1, 2, 3, 4]);
        return path;
    }
}