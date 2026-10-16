using AIStudio.Models.Matching;

using Microsoft.CodeAnalysis;

using SourceCodeRules.UsageAnalyzers;

namespace AIStudio.Tests.Models.Generation;

/// <summary>
/// Checks that a pattern which can never match is refused while compiling.
/// </summary>
/// <remarks>
/// A pattern carrying a capital letter, an underscore, or a space matches no model name, because
/// names are normalized before any rule sees them. At runtime that looks like nothing: the family
/// answers for nobody and its models quietly take the global default. MWAIS0013 turns it into a
/// build error, and these tests are what says that it actually recognizes the calls it is meant to.
/// </remarks>
[TestFixture]
public sealed class ModelPatternLiteralAnalyzerTests
{
    /// <summary>
    /// Patterns and whether the app considers them normalized, checked from both ends.
    /// </summary>
    /// <remarks>
    /// The analyzer carries its own copy of the normalization, because it cannot reference the app.
    /// This is the table which keeps the two honest: whatever MatchPattern.IsNormalized says at
    /// runtime, the compile time rule has to say the same.
    /// </remarks>
    private static readonly string[] PATTERNS_TO_AGREE_ON =
    [
        "gpt-5.1", "qwen3.8-27b", "deepseek-r1", "yi", "01",
        "GPT-5.1", "gpt_5", "gpt 5", "gpt--5", "-gpt-5", "gpt-5-", "Qwen3.8:27B", "___",
    ];

    [Test]
    public async Task APatternWrittenTheWayNamesArriveIsAccepted()
    {
        var reported = await AnalyzeAsync("""builder.Rule("gpt-5.1").AsPrefix();""");

        Assert.That(reported, Is.Empty);
    }

    [TestCase("""builder.Rule("GPT-5.1");""", "gpt-5.1")]
    [TestCase("""builder.Rule("gpt_5");""", "gpt-5")]
    [TestCase("""builder.Rule("gpt 5");""", "gpt-5")]
    [TestCase("""builder.Modifier("BASE");""", "base")]
    [TestCase("""builder.Rule("gpt-5").AlsoContains("Codex");""", "codex")]
    [TestCase("""builder.Rule("gpt-5").NotContains("Chat");""", "chat")]
    [TestCase("""builder.Rule("gpt-5"); builder.Rule("gpt-5-mini").InheritsFrom("GPT-5");""", "gpt-5")]
    public async Task APatternWhichCanNeverMatchIsRefusedAndTheRightSpellingIsNamed(string statements, string expectedSpelling)
    {
        var reported = await AnalyzeAsync(statements);

        Assert.Multiple(() =>
        {
            Assert.That(reported, Has.Count.EqualTo(1));
            Assert.That(reported.FirstOrDefault()?.Id, Is.EqualTo("MWAIS0013"));
            Assert.That(reported.FirstOrDefault()?.GetMessage(), Does.Contain($"write it as \"{expectedSpelling}\""));
        });
    }

    [Test]
    public async Task APatternOfWhichNothingSurvivesSaysThatInsteadOfSuggestingAnEmptyOne()
    {
        var reported = await AnalyzeAsync("""builder.Rule("___");""");

        Assert.Multiple(() =>
        {
            Assert.That(reported, Has.Count.EqualTo(1));
            Assert.That(reported.FirstOrDefault()?.GetMessage(), Does.Contain("nothing of it survives"));
        });
    }

    [Test]
    public async Task APatternWrittenOnceAsAConstantIsCheckedToo()
    {
        var reported = await AnalyzeAsync("""const string THE_PATTERN = "GPT-5"; builder.Rule(THE_PATTERN);""");

        Assert.That(reported, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task TextWhichIsNotAPatternIsLeftAlone()
    {
        //
        // A tokenizer is named the way its vendor names it, and o200k_base carries an underscore
        // because OpenAI writes it that way. An analyzer which cannot tell the two kinds of string
        // apart would make it impossible to state the truth.
        //
        var reported = await AnalyzeAsync("""builder.Rule("gpt-5.1").Tokenizer(TokenizerKind.TIKTOKEN, "o200k_base");""");

        Assert.That(reported, Is.Empty);
    }

    [Test]
    public async Task TheCompileTimeRuleAndTheRuntimeCheckNeverDisagree()
    {
        foreach (var pattern in PATTERNS_TO_AGREE_ON)
        {
            var reported = await AnalyzeAsync($"""builder.Rule("{pattern}");""");
            var acceptedWhileCompiling = reported.Count is 0;

            Assert.That(acceptedWhileCompiling, Is.EqualTo(MatchPattern.IsNormalized(pattern)), $"The two normalizations disagree about \"{pattern}\".");
        }
    }

    private static async Task<IReadOnlyList<Diagnostic>> AnalyzeAsync(string statements)
    {
        var source =
            $$"""
              using System;

              using AIStudio.Models;

              namespace Sample;

              public sealed class SampleFamily : ModelFamily
              {
                  public override ModelVendor Vendor => ModelVendor.OPEN_AI;

                  public override ModelSource Source => new("https://example.invalid", new DateOnly(2026, 9, 11), "a note");

                  protected override void Declare(ModelFamilyBuilder builder)
                  {
                      {{statements}}
                  }
              }
              """;

        return await CompilationHarness.AnalyzeAsync(source, new ModelPatternLiteralAnalyzer());
    }
}