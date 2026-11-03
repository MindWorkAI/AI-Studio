using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools;
using AIStudio.Tools.Services;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Settings;

/// <summary>
/// Checks which transcription providers may hear the audio and video files of a data source.
/// </summary>
/// <remarks>
/// The transcription provider receives the content of a data source just like the embedding
/// provider. A data source which asks for a high confidence level must not send its recordings to a
/// provider it would never let embed its documents.
/// </remarks>
[TestFixture]
public sealed class TranscriptionConfidenceTests
{
    private static IEnumerable<IIndexedDataSource> DataSources()
    {
        foreach (var level in Enum.GetValues<ConfidenceLevel>())
        {
            yield return new DataSourceLocalDirectory { ConfidenceLevel = level };
            yield return new DataSourceLocalFile { ConfidenceLevel = level };
            yield return new DataSourceMailbox { ConfidenceLevel = level };
        }
    }

    [TestCaseSource(nameof(DataSources))]
    public void TranscriptionFollowsTheRuleOfTheEmbedding(IIndexedDataSource dataSource)
    {
        foreach (var providerLevel in Enum.GetValues<ConfidenceLevel>())
            Assert.That(DataSourceEmbeddingService.AllowsTranscription(dataSource, providerLevel), Is.EqualTo(DataSourceEmbeddingService.AllowsEmbedding(dataSource, providerLevel)), $"A provider with the level {providerLevel} is treated differently for {dataSource.GetType().Name} with the level {dataSource.ConfidenceLevel}.");
    }

    [Test]
    public void AFolderWhichAsksForAHighLevelKeepsItsRecordingsFromAMediumProvider()
    {
        var directory = new DataSourceLocalDirectory { ConfidenceLevel = ConfidenceLevel.HIGH };
        using (Assert.EnterMultipleScope())
        {
            Assert.That(DataSourceEmbeddingService.AllowsTranscription(directory, ConfidenceLevel.MEDIUM), Is.False);
            Assert.That(DataSourceEmbeddingService.AllowsTranscription(directory, ConfidenceLevel.HIGH), Is.True);
        }
    }

    [Test]
    public void AFolderWithoutALevelStaysOpenToEveryTranscriptionProvider()
    {
        var directory = new DataSourceLocalDirectory { ConfidenceLevel = ConfidenceLevel.NONE };
        Assert.That(DataSourceEmbeddingService.AllowsTranscription(directory, ConfidenceLevel.UNTRUSTED), Is.True);
    }

    [Test]
    public void ATranscriptionProviderHasTheLevelOfItsCompany()
    {
        var settingsManager = new SettingsManager(NullLogger<SettingsManager>.Instance, null!);
        settingsManager.ConfigurationData.Confidence.ConfidenceScheme = ConfidenceSchemes.CUSTOM;
        settingsManager.ConfigurationData.Confidence.CustomConfidenceScheme[LLMProviders.OPEN_AI] = ConfidenceLevel.LOW;

        var provider = new TranscriptionProvider { Id = Guid.NewGuid().ToString(), UsedLLMProvider = LLMProviders.OPEN_AI };
        Assert.That(provider.GetConfidenceLevel(settingsManager), Is.EqualTo(ConfidenceLevel.LOW));
    }

    [Test]
    public void NoTranscriptionProviderHasNoConfidence()
    {
        var settingsManager = new SettingsManager(NullLogger<SettingsManager>.Instance, null!);
        Assert.That(TranscriptionProvider.NONE.GetConfidenceLevel(settingsManager), Is.EqualTo(ConfidenceLevel.NONE));
    }
}