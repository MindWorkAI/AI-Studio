using AIStudio.Tests.Models.Generation;

using SourceCodeRules.UsageAnalyzers;
using SourceCodeRules.UsageCodeFixes;

namespace AIStudio.Tests.Analyzers;

/// <summary>
/// Checks that MWAIS0018 refuses the line break of the system and that its code fix writes LF instead.
/// </summary>
/// <remarks>
/// StringBuilder.AppendLine and Environment.NewLine write CR LF on Windows. The pipeline runs the tests on Linux
/// only, where both write LF, so a text which differs on Windows shows up nowhere but on the computer of a developer.
/// </remarks>
[TestFixture]
public sealed class SystemLineBreakAnalyzerTests
{
    private const string USINGS = """
                                  using System;
                                  using System.Text;
                                  """;

    private const string USINGS_WITH_APPEND_LINE_LF = """
                                                      using System;
                                                      using System.Text;
                                                      using SharedTools.Text;
                                                      """;

    [TestCase("return new StringBuilder().AppendLine().ToString();", "StringBuilder.AppendLine", TestName = "AppendLine without a text")]
    [TestCase("""return new StringBuilder().AppendLine("Text").ToString();""", "StringBuilder.AppendLine", TestName = "AppendLine with a text")]
    [TestCase("""return new StringBuilder().AppendLine($"Number {number}").ToString();""", "StringBuilder.AppendLine", TestName = "AppendLine with an interpolated text")]
    [TestCase("""return $"First{Environment.NewLine}Second";""", "Environment.NewLine", TestName = "Environment.NewLine in an interpolated text")]
    [TestCase("return string.Join(Environment.NewLine, items);", "Environment.NewLine", TestName = "Environment.NewLine as a separator")]
    [TestCase("""return "First" + System.Environment.NewLine;""", "Environment.NewLine", TestName = "Environment.NewLine with its namespace")]
    public async Task TheLineBreakOfTheSystemIsRefused(string statement, string name)
    {
        var reported = await CompilationHarness.AnalyzeAsync(ClassWith(USINGS, statement), new SystemLineBreakAnalyzer());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(reported, Has.Count.EqualTo(1));
            Assert.That(reported.FirstOrDefault()?.Id, Is.EqualTo("MWAIS0018"));
            Assert.That(reported.FirstOrDefault()?.GetMessage(), Does.Contain($"'{name}'"));
        }
    }

    [TestCase("""return new StringBuilder().AppendLineLF("Text").ToString();""", TestName = "AppendLineLF")]
    [TestCase("""return new StringBuilder().Append("Text").Append('\n').ToString();""", TestName = "An LF of its own")]
    [TestCase("""return new Lines().AppendLine("Text");""", TestName = "A method AppendLine of another type")]
    [TestCase("return Console.Out.NewLine;", TestName = "The line break of a writer")]
    public async Task AnythingElseIsLeftAlone(string statement)
    {
        var reported = await CompilationHarness.AnalyzeAsync(ClassWith(USINGS_WITH_APPEND_LINE_LF, statement), new SystemLineBreakAnalyzer());

        Assert.That(reported, Is.Empty);
    }

    [Test]
    public async Task TheCodeFixUsesAppendLineLFWhereItIsInScope()
    {
        var source = ClassWith(USINGS_WITH_APPEND_LINE_LF, """return new StringBuilder().AppendLine("Text").ToString();""");
        var expected = ClassWith(USINGS_WITH_APPEND_LINE_LF, """return new StringBuilder().AppendLineLF("Text").ToString();""");

        var fixedSource = await CompilationHarness.FixAsync(source, new SystemLineBreakAnalyzer(), new SystemLineBreakCodeFixProvider());

        Assert.That(fixedSource.ReplaceLineEndings("\n"), Is.EqualTo(expected.ReplaceLineEndings("\n")), "The using directive is there already, so the fix renames the call and adds nothing.");
    }

    [Test]
    public async Task TheCodeFixAddsTheUsingDirectiveWhereItIsMissing()
    {
        var source = ClassWith(USINGS, """return new StringBuilder().AppendLine($"Number {number}").ToString();""");
        var expected = ClassWith(USINGS_WITH_APPEND_LINE_LF, """return new StringBuilder().AppendLineLF($"Number {number}").ToString();""");

        var fixedSource = await CompilationHarness.FixAsync(source, new SystemLineBreakAnalyzer(), new SystemLineBreakCodeFixProvider());

        Assert.That(fixedSource.ReplaceLineEndings("\n"), Is.EqualTo(expected.ReplaceLineEndings("\n")));
    }

    [Test]
    public async Task TheCodeFixWritesAStringWithLfForEnvironmentNewLine()
    {
        var source = ClassWith(USINGS, "return string.Join(Environment.NewLine, items);");
        var expected = ClassWith(USINGS, """return string.Join("\n", items);""");

        var fixedSource = await CompilationHarness.FixAsync(source, new SystemLineBreakAnalyzer(), new SystemLineBreakCodeFixProvider());

        Assert.That(fixedSource.ReplaceLineEndings("\n"), Is.EqualTo(expected.ReplaceLineEndings("\n")));
    }

    private static string ClassWith(string usings, string statement) =>
        $$"""
          {{usings}}

          namespace Sample;

          public sealed class Lines
          {
              public string AppendLine(string text) => text;
          }

          public static class Texts
          {
              public static string Build(string[] items, int number)
              {
                  {{statement}}
              }
          }
          """;
}