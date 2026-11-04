using AIStudio.Tests.Models.Generation;

using SourceCodeRules.StyleAnalyzers;
using SourceCodeRules.StyleCodeFixes;

namespace AIStudio.Tests.Analyzers;

/// <summary>
/// Checks that methods and properties which only return a switch expression start it on the line of their arrow,
/// and that the code fix of MWAIS0006 keeps everything else of the member.
/// </summary>
/// <remarks>
/// Extension blocks turn many such methods into properties, so the rule has to know properties as well: a
/// getter which does nothing but return a switch expression reads like the method it replaced.
/// </remarks>
[TestFixture]
public sealed class SwitchExpressionMethodAnalyzerTests
{
    private const string SWITCH = "this.Kind switch { Kind.CALL => \"Call\", _ => \"Meeting\" }";

    [TestCase("public string Label() { return " + SWITCH + "; }", "Method", TestName = "A method which returns a switch expression from a block")]
    [TestCase("public string Label() =>\n" + SWITCH + ";", "Method", TestName = "A method whose switch expression starts on the next line")]
    [TestCase("public string Label =>\n" + SWITCH + ";", "Property", TestName = "A property whose switch expression starts on the next line")]
    [TestCase("public string Label { get { return " + SWITCH + "; } }", "Property", TestName = "A getter which returns a switch expression from a block")]
    [TestCase("public string Label { get => " + SWITCH + "; }", "Property", TestName = "A getter with a switch expression as its body")]
    public async Task AMemberOfAClassIsRefused(string member, string kind)
    {
        var reported = await CompilationHarness.AnalyzeAsync(ClassWith(member), new SwitchExpressionMethodAnalyzer());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(reported, Has.Count.EqualTo(1));
            Assert.That(reported.FirstOrDefault()?.Id, Is.EqualTo("MWAIS0006"));
            Assert.That(reported.FirstOrDefault()?.GetMessage(), Does.StartWith($"{kind} with a switch expression"));
        }
    }

    [TestCase("public string Label =>\nkind switch { Kind.CALL => \"Call\", _ => \"Meeting\" };", TestName = "An extension property whose switch expression starts on the next line")]
    [TestCase("public string Label { get { return kind switch { Kind.CALL => \"Call\", _ => \"Meeting\" }; } }", TestName = "The getter of an extension property which returns a switch expression from a block")]
    [TestCase("public string Label() { return kind switch { Kind.CALL => \"Call\", _ => \"Meeting\" }; }", TestName = "An extension method which returns a switch expression from a block")]
    public async Task AMemberOfAnExtensionBlockIsRefused(string member)
    {
        var reported = await CompilationHarness.AnalyzeAsync(ExtensionBlockWith(member), new SwitchExpressionMethodAnalyzer());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(reported, Has.Count.EqualTo(1));
            Assert.That(reported.FirstOrDefault()?.Id, Is.EqualTo("MWAIS0006"));
        }
    }

    [TestCase("public string Label() => " + SWITCH + ";", TestName = "A method with the switch expression on the line of its arrow")]
    [TestCase("public string Label => " + SWITCH + ";", TestName = "A property with the switch expression on the line of its arrow")]
    [TestCase("public string Label { get { return " + SWITCH + "; } set { } }", TestName = "A property with a getter and a setter")]
    [TestCase("public string Label { get { var kind = this.Kind; return kind switch { Kind.CALL => \"Call\", _ => \"Meeting\" }; } }", TestName = "A getter which does more than return a switch expression")]
    [TestCase("public string Label() { return \"Meeting\"; }", TestName = "A method which returns no switch expression")]
    public async Task AnythingElseIsLeftAlone(string member)
    {
        var reported = await CompilationHarness.AnalyzeAsync(ClassWith(member), new SwitchExpressionMethodAnalyzer());

        Assert.That(reported, Is.Empty);
    }

    [Test]
    public async Task TheCodeFixKeepsTheAttributesTypeParametersAndConstraintsOfAMethod()
    {
        var source = ClassWith(
            """
                /// <summary>Names the hash of a value.</summary>
                [System.Diagnostics.Contracts.Pure]
                public static string Label<T>(T value) where T : struct
                {
                    return value.GetHashCode() switch
                    {
                        0 => "Zero",
                        _ => "Other",
                    };
                }
            """);

        var expected = ClassWith(
            """
                /// <summary>Names the hash of a value.</summary>
                [System.Diagnostics.Contracts.Pure]
                public static string Label<T>(T value) where T : struct => value.GetHashCode() switch
                {
                    0 => "Zero",
                    _ => "Other",
                };
            """);

        var fixedSource = await CompilationHarness.FixAsync(source, new SwitchExpressionMethodAnalyzer(), new SwitchExpressionMethodCodeFixProvider());

        Assert.That(fixedSource.ReplaceLineEndings("\n"), Is.EqualTo(expected.ReplaceLineEndings("\n")));
    }

    [Test]
    public async Task TheCodeFixTurnsAGetterIntoAnExpressionBody()
    {
        var source = ClassWith(
            """
                public string Label
                {
                    get
                    {
                        return this.Kind switch
                        {
                            Kind.CALL => "Call",
                            _ => "Meeting",
                        };
                    }
                }
            """);

        var expected = ClassWith(
            """
                public string Label => this.Kind switch
                {
                    Kind.CALL => "Call",
                    _ => "Meeting",
                };
            """);

        var fixedSource = await CompilationHarness.FixAsync(source, new SwitchExpressionMethodAnalyzer(), new SwitchExpressionMethodCodeFixProvider());

        Assert.That(fixedSource.ReplaceLineEndings("\n"), Is.EqualTo(expected.ReplaceLineEndings("\n")));
    }

    [Test]
    public async Task TheCodeFixMovesTheSwitchOfAnExtensionPropertyOntoTheLineOfItsArrow()
    {
        var source = ExtensionBlockWith(
            """
                    public string Label =>
                        kind switch
                        {
                            Kind.CALL => "Call",
                            _ => "Meeting",
                        };
            """);

        var expected = ExtensionBlockWith(
            """
                    public string Label => kind switch
                    {
                        Kind.CALL => "Call",
                        _ => "Meeting",
                    };
            """);

        var fixedSource = await CompilationHarness.FixAsync(source, new SwitchExpressionMethodAnalyzer(), new SwitchExpressionMethodCodeFixProvider());

        Assert.That(fixedSource.ReplaceLineEndings("\n"), Is.EqualTo(expected.ReplaceLineEndings("\n")));
    }

    private static string ClassWith(string members) =>
        $$"""
          namespace Sample;

          public enum Kind
          {
              CALL,
              MEETING,
          }

          public sealed class Meeting
          {
              public Kind Kind { get; set; } = Kind.MEETING;

          {{members}}
          }
          """;

    private static string ExtensionBlockWith(string members) =>
        $$"""
          namespace Sample;

          public enum Kind
          {
              CALL,
              MEETING,
          }

          public static class KindExtensions
          {
              extension(Kind kind)
              {
          {{members}}
              }
          }
          """;
}
