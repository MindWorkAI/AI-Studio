using AIStudio.Assistants.BeyondFactsLab;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Tools;
using AIStudio.Tools.Services;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.BeyondFactsLab;

/// <summary>
/// Checks how the BeyondFacts Lab turns recordings into material: who may hear them, and where their transcripts wait.
/// </summary>
[TestFixture]
public sealed class BflRecordingTests
{
    private string dataDirectory = string.Empty;
    private SettingsManager settingsManager = null!;
    private MediaTranscriptionService transcriptionService = null!;

    [SetUp]
    public void PrepareSettings()
    {
        this.dataDirectory = Path.Combine(Path.GetTempPath(), $"ai-studio-beyondfacts-lab-recordings-{Guid.NewGuid():N}");
        Directory.CreateDirectory(this.dataDirectory);

        this.settingsManager = new SettingsManager(NullLogger<SettingsManager>.Instance, null!);
        var transcriptionProvider = new TranscriptionProvider { Id = Guid.NewGuid().ToString(), Name = "Transcription", UsedLLMProvider = LLMProviders.OPEN_AI };
        this.settingsManager.ConfigurationData.TranscriptionProviders.Add(transcriptionProvider);
        this.settingsManager.ConfigurationData.App.UseTranscriptionProvider = transcriptionProvider.Id;
        this.settingsManager.ConfigurationData.Confidence.ConfidenceScheme = ConfidenceSchemes.CUSTOM;

        // Finding the provider reads only the settings, so the other services stay out of these tests:
        this.transcriptionService = new MediaTranscriptionService(null!, this.settingsManager, null!, [], NullLogger<MediaTranscriptionService>.Instance);
    }

    [TearDown]
    public void CleanUp()
    {
        this.transcriptionService.Dispose();
        if (Directory.Exists(this.dataDirectory))
            Directory.Delete(this.dataDirectory, true);
    }

    [Test]
    public void ATranscriptionProviderBelowTheConfidenceOfTheLabHearsNoRecording()
    {
        this.settingsManager.ConfigurationData.Confidence.CustomConfidenceScheme[LLMProviders.OPEN_AI] = ConfidenceLevel.LOW;
        var service = this.CreateService();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(this.transcriptionService.GetUsableProvider(), Is.Not.Null, "The chat may still use the provider.");
            Assert.That(service.UsableTranscriptionProvider(), Is.Null, "A recording of the lab is as confidential as a paper.");
            Assert.That(service.MinimumTranscriptionConfidence, Is.GreaterThanOrEqualTo(ConfidenceLevel.MEDIUM));
        }
    }

    [Test]
    public void ATranscriptionProviderWithTheConfidenceOfTheLabHearsRecordings()
    {
        this.settingsManager.ConfigurationData.Confidence.CustomConfidenceScheme[LLMProviders.OPEN_AI] = ConfidenceLevel.MEDIUM;

        Assert.That(this.CreateService().UsableTranscriptionProvider(), Is.Not.Null);
    }

    [Test]
    public void AProhibitedTranscriptionProviderHearsNoRecording()
    {
        this.settingsManager.ConfigurationData.Confidence.CustomConfidenceScheme[LLMProviders.OPEN_AI] = ConfidenceLevel.HIGH;
        this.settingsManager.ConfigurationData.App.ProhibitedProviders.Add(LLMProviders.OPEN_AI);

        Assert.That(this.CreateService().UsableTranscriptionProvider(), Is.Null);
    }

    [Test]
    public async Task ATranscriptWaitsInAFolderOfItsOwnBelowTheStaging()
    {
        var first = new BflTranscriptLocation(this.dataDirectory);
        var second = new BflTranscriptLocation(this.dataDirectory);

        var slot = await first.ResolveAsync("/Users/person/Interview.mp3", CancellationToken.None);
        var stagingDirectory = Path.Combine(this.dataDirectory, "media-staging");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Path.GetDirectoryName(slot.Path), Is.EqualTo(first.FolderPath));
            Assert.That(Path.GetDirectoryName(first.FolderPath), Is.EqualTo(stagingDirectory), "The cleanup at the start of AI Studio removes only what lies below the staging.");
            Assert.That(second.FolderPath, Is.Not.EqualTo(first.FolderPath), "Two imports would share one transcript.");
            Assert.That(slot.Path, Does.Not.Contain("Interview"), "The folder of the person decides nothing in the data directory.");
        }
    }

    [Test]
    public async Task DeletingTheLocationRemovesTheTranscript()
    {
        var location = new BflTranscriptLocation(this.dataDirectory);
        var slot = await location.ResolveAsync("/Users/person/Interview.mp3", CancellationToken.None);
        Directory.CreateDirectory(location.FolderPath);
        await File.WriteAllTextAsync(slot.Path, "Guten Tag.");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(location.TryDelete(), Is.True);
            Assert.That(Directory.Exists(location.FolderPath), Is.False);
            Assert.That(new BflTranscriptLocation(this.dataDirectory).TryDelete(), Is.True, "A location without a transcript is no failure.");
        }
    }

    private BflMaterialService CreateService() => new(new BeyondFactsLabStore(NullLogger<BeyondFactsLabStore>.Instance, new BflStorageOptions { DataDirectory = this.dataDirectory }), null!, null!, null!, this.settingsManager, this.transcriptionService, null!, NullLogger<BflMaterialService>.Instance);
}