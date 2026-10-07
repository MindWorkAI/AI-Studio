using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks which data sources the warnings about an embedding provider name.
/// </summary>
/// <remarks>
/// Mailboxes live in a list of their own, next to the data sources. An embedding provider serves
/// both, so deleting it costs a mailbox just as much, and the warning has to say so.
/// </remarks>
[TestFixture]
public sealed class DataSourceReindexWarningTests
{
    private const string EMBEDDING_ID = "5c0e8a4f-2d6b-4e1a-9f3c-7b8d9e0a1b2c";

    [Test]
    public void DeletingAnEmbeddingProviderNamesTheMailboxesUsingIt()
    {
        var settingsManager = new SettingsManager(NullLogger<SettingsManager>.Instance, null!);
        var embeddingProvider = new EmbeddingProvider() with { Id = EMBEDDING_ID, Name = "Local embedding" };

        settingsManager.ConfigurationData.DataSources.Add(new DataSourceLocalDirectory { Id = Guid.NewGuid().ToString(), Name = "Reports", Type = DataSourceType.LOCAL_DIRECTORY, EmbeddingId = EMBEDDING_ID });
        settingsManager.ConfigurationData.Mailboxes.Add(new DataSourceMailbox { Id = Guid.NewGuid().ToString(), Name = "Work mail", EmbeddingId = EMBEDDING_ID });
        settingsManager.ConfigurationData.Mailboxes.Add(new DataSourceMailbox { Id = Guid.NewGuid().ToString(), Name = "Private mail", EmbeddingId = Guid.NewGuid().ToString() });

        var description = DataSourceReindexWarning.DescribeDataSourcesLosingTheirProvider(settingsManager, embeddingProvider);
        Assert.Multiple(() =>
        {
            Assert.That(description, Does.Contain("- Reports"), "The data source using the provider is not named.");
            Assert.That(description, Does.Contain("- Work mail"), "The mailbox using the provider is not named.");
            Assert.That(description, Does.Not.Contain("Private mail"), "A mailbox using another provider is named.");
        });
    }
}