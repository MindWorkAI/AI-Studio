using System.Text.Json;

using AIStudio.Chat;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.RAG;

namespace AIStudio.Tests.Settings;

/// <summary>
/// Checks where a chat may still send data once passages of a data source reached it.
/// </summary>
/// <remarks>
/// A data source holds the documents of the user or of their organization. Unless somebody chose
/// otherwise, a chat which found something in it must not carry that out in a web search. An
/// organization may rule out the less strict levels, and a data source which only self-hosted
/// providers may use allows the configured services only. Both hold for data sources nobody edits
/// again, so they hold when the passages reach a chat, not only in the dialog.
/// </remarks>
[TestFixture]
public sealed class DataSourceOutboundDataRestrictionTests
{
    private static readonly IDataSource OPEN_FOLDER = new DataSourceLocalDirectory
    {
        Id = "4b7e1c9a-2d5f-4a8e-b3c6-9f1d7e2a5c84",
        Name = "Open folder",
        OutboundDataRestriction = OutboundDataRestriction.UNRESTRICTED,
    };

    private static readonly IDataSource CLOSED_FILE = new DataSourceLocalFile
    {
        Id = "7c2f9e4b-1a6d-4e3c-8b5a-2d9f6c1e4a73",
        Name = "Closed file",
        OutboundDataRestriction = OutboundDataRestriction.ONLY_CONFIGURED_SERVICES,
    };

    private static readonly IDataSource SELF_HOSTED_SERVER = new DataSourceERI_V1
    {
        Id = "9e4a2c7f-5b1d-4f6e-a8c3-1d7b4e9f2a65",
        Name = "Server for self-hosted providers",
        SecurityPolicy = DataSourceSecurity.SELF_HOSTED,
        OutboundDataRestriction = OutboundDataRestriction.UNRESTRICTED,
    };

    private static readonly IDataSource OPEN_SERVER = new DataSourceERI_V1
    {
        Id = "2a8d5f1c-7e3b-4c9a-b6d2-5f8e1a3c7b49",
        Name = "Server for any provider",
        SecurityPolicy = DataSourceSecurity.ALLOW_ANY,
        OutboundDataRestriction = OutboundDataRestriction.UNRESTRICTED,
    };

    [Test]
    public void ANewDataSourceAllowsOnlyLinksFromTheChat()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(new DataSourceLocalFile().OutboundDataRestriction, Is.EqualTo(OutboundDataRestriction.ONLY_LINKS_FROM_CHAT));
            Assert.That(new DataSourceLocalDirectory().OutboundDataRestriction, Is.EqualTo(OutboundDataRestriction.ONLY_LINKS_FROM_CHAT));
            Assert.That(new DataSourceERI_V1().OutboundDataRestriction, Is.EqualTo(OutboundDataRestriction.ONLY_LINKS_FROM_CHAT));
        }
    }

    [TestCase(nameof(DataSourceType.LOCAL_FILE))]
    [TestCase(nameof(DataSourceType.LOCAL_DIRECTORY))]
    [TestCase(nameof(DataSourceType.ERI_V1))]
    public void ADataSourceStoredBeforeItHadALevelAllowsOnlyLinksFromTheChat(string type)
    {
        var dataSource = JsonSerializer.Deserialize<IDataSource>($$"""{"$type_discriminator": "{{type}}", "Name": "Older data source"}""", SettingsManager.JSON_OPTIONS);

        Assert.That(dataSource?.OutboundDataRestriction, Is.EqualTo(OutboundDataRestriction.ONLY_LINKS_FROM_CHAT), "Existing data sources get the level of new ones.");
    }

    [Test]
    public void ALevelThisVersionCannotReadFallsBackToTheSafeSide()
    {
        var dataSource = JsonSerializer.Deserialize<IDataSource>("""{"$type_discriminator": "LOCAL_DIRECTORY", "OutboundDataRestriction": "NOTHING_AT_ALL"}""", SettingsManager.JSON_OPTIONS);

        Assert.That(dataSource?.OutboundDataRestriction, Is.EqualTo(OutboundDataRestriction.ONLY_CONFIGURED_SERVICES), "A newer version may know levels this one does not.");
    }

    [Test]
    public void ANumberWhichNamesNoLevelRestrictsTheChatToTheConfiguredServices()
    {
        var dataSource = JsonSerializer.Deserialize<IDataSource>("""{"$type_discriminator": "LOCAL_DIRECTORY", "Id": "6f3a9d2e-8c1b-4e7a-b5d4-3a9e7f1c2b68", "OutboundDataRestriction": "5"}""", SettingsManager.JSON_OPTIONS)!;
        var chat = new ChatThread();

        chat.RequireOutboundDataRestriction(DataSourceRequirements.Of([dataSource], [2], OutboundDataRestriction.UNRESTRICTED).OutboundData);

        Assert.That(chat.RequiredOutboundDataRestriction, Is.EqualTo(new OutboundDataRequirement(OutboundDataRestriction.ONLY_CONFIGURED_SERVICES, OutboundDataOrigin.DATA_SOURCE, dataSource.Id)), "The settings read the number as it is, and compared as it is, it would rank above UNRESTRICTED.");
    }

    [Test]
    public void TheLevelSurvivesSavingTheSettings()
    {
        var loaded = JsonSerializer.Deserialize<IDataSource>(JsonSerializer.Serialize(OPEN_FOLDER, SettingsManager.JSON_OPTIONS), SettingsManager.JSON_OPTIONS);

        Assert.That(loaded?.OutboundDataRestriction, Is.EqualTo(OutboundDataRestriction.UNRESTRICTED));
    }

    [Test]
    public void WithoutAnOrganizationTheUserDecides()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(new DataSourceSecuritySettings().MinimumOutboundDataRestriction, Is.EqualTo(OutboundDataRestriction.UNRESTRICTED));
            Assert.That(OPEN_FOLDER.GetEffectiveOutboundDataRestriction(OutboundDataRestriction.UNRESTRICTED), Is.EqualTo(OutboundDataRestriction.UNRESTRICTED));
        }
    }

    [Test]
    public void TheMinimumTightensALooserDataSource() =>
        Assert.That(OPEN_FOLDER.GetEffectiveOutboundDataRestriction(OutboundDataRestriction.ONLY_LINKS_FROM_CHAT), Is.EqualTo(OutboundDataRestriction.ONLY_LINKS_FROM_CHAT));

    [Test]
    public void TheMinimumNeverLoosensAStricterDataSource() =>
        Assert.That(CLOSED_FILE.GetEffectiveOutboundDataRestriction(OutboundDataRestriction.ONLY_LINKS_FROM_CHAT), Is.EqualTo(OutboundDataRestriction.ONLY_CONFIGURED_SERVICES));

    [Test]
    public void ADataSourceForSelfHostedProvidersAllowsOnlyTheConfiguredServices()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(SELF_HOSTED_SERVER.GetEffectiveOutboundDataRestriction(OutboundDataRestriction.UNRESTRICTED), Is.EqualTo(OutboundDataRestriction.ONLY_CONFIGURED_SERVICES), "Stored before, or provided by a configuration plugin, its level never went through the dialog.");
            Assert.That(OPEN_SERVER.GetEffectiveOutboundDataRestriction(OutboundDataRestriction.UNRESTRICTED), Is.EqualTo(OutboundDataRestriction.UNRESTRICTED));
        }
    }

    [TestCase(DataSourceSecurity.NOT_SPECIFIED, OutboundDataRestriction.UNRESTRICTED, OutboundDataRestriction.UNRESTRICTED)]
    [TestCase(DataSourceSecurity.ALLOW_ANY, OutboundDataRestriction.UNRESTRICTED, OutboundDataRestriction.UNRESTRICTED)]
    [TestCase(DataSourceSecurity.ALLOW_ANY, OutboundDataRestriction.ONLY_LINKS_FROM_CHAT, OutboundDataRestriction.ONLY_LINKS_FROM_CHAT)]
    [TestCase(DataSourceSecurity.SELF_HOSTED, OutboundDataRestriction.UNRESTRICTED, OutboundDataRestriction.ONLY_CONFIGURED_SERVICES)]
    [TestCase(DataSourceSecurity.SELF_HOSTED, OutboundDataRestriction.ONLY_LINKS_FROM_CHAT, OutboundDataRestriction.ONLY_CONFIGURED_SERVICES)]
    public void TheLeastStrictLevelFollowsTheOrganizationAndTheSecurityPolicy(DataSourceSecurity securityPolicy, OutboundDataRestriction organizationMinimum, OutboundDataRestriction expected) =>
        Assert.That(DataSourceOutboundDataExtensions.GetMinimumOutboundDataRestriction(securityPolicy, organizationMinimum), Is.EqualTo(expected));
}