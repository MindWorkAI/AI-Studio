namespace AIStudio.Tests.Serialization;

/// <summary>
/// Checks that the contracts AI Studio uses for its JSON match those reflection builds.
/// </summary>
/// <remarks>
/// AI Studio wrote all its files with contracts which reflection built. Any other contract, e.g., one which
/// source generation builds, has to match them in every detail, or AI Studio no longer reads what it wrote
/// before: a property with another name, without a setter, or without its converter silently loses its value.
/// </remarks>
[TestFixture]
public sealed class JsonContractTests
{
    [TestCaseSource(typeof(JsonTestRoots), nameof(JsonTestRoots.Stored))]
    public void TheContractsMatchThoseOfReflection(JsonTestRoot root)
    {
        var baseline = JsonReflectionBaseline.Of(root.Options);
        var expected = JsonTypeGraph.Collect(root.Type, baseline).Select(type => JsonContractDescription.Describe(baseline.GetTypeInfo(type)));
        var actual = JsonTypeGraph.Collect(root.Type, root.Options).Select(type => JsonContractDescription.Describe(root.Options.GetTypeInfo(type)));

        Assert.That(actual, Is.EqualTo(expected));
    }
}