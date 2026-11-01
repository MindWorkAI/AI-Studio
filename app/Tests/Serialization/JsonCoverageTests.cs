using System.Text.Json.Serialization;

namespace AIStudio.Tests.Serialization;

/// <summary>
/// Checks that every contract AI Studio uses for its JSON comes from source generation, never from reflection.
/// </summary>
/// <remarks>
/// A type which no context of the options knows makes System.Text.Json throw, unless the options fall back to
/// reflection. Such a fallback hides the missing type until a trimmed build removes what reflection needs, and
/// only a published build shows it then.
/// </remarks>
[TestFixture]
public sealed class JsonCoverageTests
{
    [TestCaseSource(typeof(JsonTestRoots), nameof(JsonTestRoots.Stored))]
    public void EveryContractComesFromSourceGeneration(JsonTestRoot root)
    {
        var typesWithoutGeneratedContract = JsonTypeGraph.Collect(root.Type, root.Options)
            .Where(type => root.Options.GetTypeInfo(type).OriginatingResolver is not JsonSerializerContext)
            .Select(JsonContractDescription.TypeName);

        Assert.That(typesWithoutGeneratedContract, Is.Empty);
    }
}