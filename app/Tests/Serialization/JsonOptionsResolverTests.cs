using System.Text.Json;
using System.Text.Json.Serialization;

using AIStudio.TrimSmokeTest;

namespace AIStudio.Tests.Serialization;

/// <summary>
/// Checks that all options of the app get their contracts from source generation, never from reflection.
/// </summary>
/// <remarks>
/// MWAIS0015 refuses options without a TypeInfoResolver while compiling, but cannot tell what the resolver does.
/// Only contexts throw for a type they do not know. Any other resolver, e.g., a DefaultJsonTypeInfoResolver which
/// a helper adds to the chain, builds the missing contract by reflection instead, and in a published build, the
/// code it needs for that may be gone.
/// </remarks>
[TestFixture]
public sealed class JsonOptionsResolverTests
{
    private static IEnumerable<TestCaseData> AllOptions()
    {
        TestHost.PrepareStaticState();
        return AppJsonOptions.FindAll().Select(options => new TestCaseData(options.Value).SetArgDisplayNames(options.Key));
    }

    [TestCaseSource(nameof(AllOptions))]
    public void TheOptionsResolveWithContextsOnly(JsonSerializerOptions options)
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(options.TypeInfoResolverChain, Is.Not.Empty);
            Assert.That(options.TypeInfoResolverChain, Is.All.InstanceOf<JsonSerializerContext>());
        }
    }

    [Test]
    public void FieldsAndPropertiesOfTheAppAreFound()
    {
        TestHost.PrepareStaticState();
        var names = AppJsonOptions.FindAll().Keys.ToList();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(names, Does.Contain("SettingsManager.JSON_OPTIONS"));
            Assert.That(names, Does.Contain("VisualBriefingJson.Canonical"));
            Assert.That(names, Has.None.Contains("k__BackingField"));
        }
    }
}