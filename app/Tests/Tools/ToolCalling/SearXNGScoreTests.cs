using System.Globalization;
using System.Text.Json.Nodes;

using AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.WebSearch.SearXNG;

namespace AIStudio.Tests.Tools.ToolCalling;

/// <summary>
/// Checks that the score of a SearXNG result is read the same way on every computer.
/// </summary>
/// <remarks>
/// The score decides the order of the results. An instance may send it as a number or as text, both in the notation
/// of JSON. All cases run under de-DE, where the period separates thousands: read with that culture, "0.75" was 75.
/// </remarks>
[TestFixture]
[SetCulture("de-DE")]
public sealed class SearXNGScoreTests
{
    [Test]
    public void TheCultureReadsThePeriodAsAThousandsSeparator()
    {
        Assert.That(double.Parse("0.75", CultureInfo.CurrentCulture), Is.EqualTo(75), "Without that, the other cases check nothing.");
    }

    [TestCase("""{ "score": 0.75 }""")]
    [TestCase("""{ "score": "0.75" }""")]
    public void TheScoreIsReadTheSameOnEveryComputer(string json)
    {
        var result = JsonNode.Parse(json)!.AsObject();

        var found = SearXNGSearchClient.TryGetScore(result, out var score);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(found, Is.True);
            Assert.That(score, Is.EqualTo(0.75));
        }
    }
}