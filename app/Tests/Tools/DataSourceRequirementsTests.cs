using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.RAG;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks what a chat requires from now on, because passages of data sources reached the model.
/// </summary>
/// <remarks>
/// Semantic Search and the classic RAG process both ask here, so that both demand the same of the
/// same data sources. Only the data sources with passages count: one search in the wrong data
/// source must not lock a chat for good. Of several equally strict data sources, the chat names
/// the same one, whichever way searched them and in whichever order.
/// </remarks>
[TestFixture]
public sealed class DataSourceRequirementsTests
{
    private static readonly IDataSource HANDBOOK = new DataSourceLocalDirectory { Num = 1, Id = "11111111-1111-1111-1111-111111111111", Name = "Handbook", MaxMatches = 10, ConfidenceLevel = ConfidenceLevel.HIGH };
    private static readonly IDataSource MINUTES = new DataSourceLocalFile { Num = 2, Id = "22222222-2222-2222-2222-222222222222", Name = "Minutes", MaxMatches = 10, ConfidenceLevel = ConfidenceLevel.LOW, OutboundDataRestriction = OutboundDataRestriction.UNRESTRICTED };
    private static readonly IDataSource INTRANET = new DataSourceERI_V1 { Num = 3, Id = "33333333-3333-3333-3333-333333333333", Name = "Intranet", MaxMatches = 10, SecurityPolicy = DataSourceSecurity.SELF_HOSTED };
    private static readonly IDataSource ARCHIVE = new DataSourceLocalDirectory { Num = 4, Id = "44444444-4444-4444-4444-444444444444", Name = "Archive", MaxMatches = 10, ConfidenceLevel = ConfidenceLevel.LOW };

    [Test]
    public void ASearchWhichFoundNothingRequiresNothing() =>
        Assert.That(DataSourceRequirements.Of([HANDBOOK, INTRANET], [0, 0], OutboundDataRestriction.UNRESTRICTED), Is.EqualTo(DataSourceRequirements.NONE));

    [Test]
    public void OnlyTheDataSourcesWithPassagesCount()
    {
        var requirements = DataSourceRequirements.Of([HANDBOOK, MINUTES, INTRANET], [0, 2, 0], OutboundDataRestriction.UNRESTRICTED);

        Assert.That(requirements, Is.EqualTo(new DataSourceRequirements(ConfidenceLevel.LOW, DataSourceSecurity.ALLOW_ANY, OutboundDataRequirement.NONE)), "Only the minutes brought something in, and they allow every tool.");
    }

    [Test]
    public void TheStrictestDataSourceDecides()
    {
        var requirements = DataSourceRequirements.Of([MINUTES, HANDBOOK, INTRANET], [1, 1, 1], OutboundDataRestriction.UNRESTRICTED);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(requirements.Confidence, Is.EqualTo(ConfidenceLevel.HIGH));
            Assert.That(requirements.Security, Is.EqualTo(DataSourceSecurity.SELF_HOSTED));
            Assert.That(requirements.OutboundData, Is.EqualTo(new OutboundDataRequirement(OutboundDataRestriction.ONLY_CONFIGURED_SERVICES, OutboundDataOrigin.DATA_SOURCE, INTRANET.Id)), "The intranet may only be seen by self-hosted providers.");
        }
    }

    [Test]
    public void OnATieTheFirstDataSourceByNumberIsNamed()
    {
        var expected = new OutboundDataRequirement(OutboundDataRestriction.ONLY_LINKS_FROM_CHAT, OutboundDataOrigin.DATA_SOURCE, HANDBOOK.Id);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(DataSourceRequirements.Of([HANDBOOK, ARCHIVE], [1, 1], OutboundDataRestriction.UNRESTRICTED).OutboundData, Is.EqualTo(expected));
            Assert.That(DataSourceRequirements.Of([ARCHIVE, HANDBOOK], [1, 1], OutboundDataRestriction.UNRESTRICTED).OutboundData, Is.EqualTo(expected), "An agent may select the data sources in another order.");
        }
    }

    [Test]
    public void AnOrganizationMinimumTightensTheDataSource()
    {
        var requirements = DataSourceRequirements.Of([MINUTES], [1], OutboundDataRestriction.ONLY_LINKS_FROM_CHAT);

        Assert.That(requirements.OutboundData, Is.EqualTo(new OutboundDataRequirement(OutboundDataRestriction.ONLY_LINKS_FROM_CHAT, OutboundDataOrigin.DATA_SOURCE, MINUTES.Id)), "The chat names the data source it found something in, even though the level comes from the organization.");
    }

    [Test]
    public void CountsWhichDoNotMatchTheDataSourcesAreRefused() =>
        Assert.That(() => DataSourceRequirements.Of([HANDBOOK, MINUTES], [1], OutboundDataRestriction.UNRESTRICTED), Throws.ArgumentException);
}