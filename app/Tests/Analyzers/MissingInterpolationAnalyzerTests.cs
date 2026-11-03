using AIStudio.Tests.Models.Generation;

using Microsoft.CodeAnalysis;

using SourceCodeRules.UsageAnalyzers;

namespace AIStudio.Tests.Analyzers;

/// <summary>
/// Checks that a string which names a member in braces without being interpolated is refused while compiling.
/// </summary>
/// <remarks>
/// The Agenda Planner asked the model for an activity at '{this.inputLocation}', word for word, because a $
/// was missing in front of the string. Nothing at runtime gives such a string away, so MWAIS0014 turns it into
/// a build error, and these tests say which strings it recognizes and which it leaves alone.
/// </remarks>
[TestFixture]
public sealed class MissingInterpolationAnalyzerTests
{
    [TestCase(""" "at '{this.location}'" """, TestName = "A regular string")]
    [TestCase(""" @"at '{this.location}'" """, TestName = "A verbatim string")]
    [TestCase("""" """at '{this.location}'""" """", TestName = "A raw string")]
    [TestCase("""" $$"""at '{this.location}'""" """", TestName = "A raw string with two dollar signs and one pair of braces")]
    [TestCase(""" $"at '{{this.location}}'" """, TestName = "An interpolated string with escaped braces")]
    public async Task AMemberInBracesWhichStaysTextIsRefused(string expression)
    {
        var reported = await AnalyzeAsync(expression);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(reported, Has.Count.EqualTo(1));
            Assert.That(reported.FirstOrDefault()?.Id, Is.EqualTo("MWAIS0014"));
            Assert.That(reported.FirstOrDefault()?.GetMessage(), Does.Contain("\"{this.location}\""));
        }
    }

    [Test]
    public async Task AMultiLineRawStringIsRefused()
    {
        var reported = await AnalyzeAsync(
            """"
            """
            The participants meet at '{this.location}'.
            """
            """");

        Assert.That(reported, Has.Count.EqualTo(1));
    }

    [TestCase(""" $"at '{this.location}'" """, TestName = "An interpolated string")]
    [TestCase("""" $$"""at '{{this.location}}'""" """", TestName = "A raw string with two dollar signs and two pairs of braces")]
    [TestCase(""" string.Format("at '{0}'", this.location) """, TestName = "A placeholder for string.Format")]
    [TestCase(""" "the {this} keyword" """, TestName = "The keyword in braces")]
    [TestCase(""" "at this.location" """, TestName = "A member without braces")]
    public async Task TextWhichIsNoForgottenInterpolationIsLeftAlone(string expression)
    {
        var reported = await AnalyzeAsync(expression);

        Assert.That(reported, Is.Empty);
    }

    private static async Task<IReadOnlyList<Diagnostic>> AnalyzeAsync(string expression)
    {
        var source =
            $$"""
              namespace Sample;

              public sealed class Meeting
              {
                  private readonly string location = "Cologne";

                  public string Describe() => {{expression}};
              }
              """;

        return await CompilationHarness.AnalyzeAsync(source, new MissingInterpolationAnalyzer());
    }
}