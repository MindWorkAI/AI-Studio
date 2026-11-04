using AIStudio.Tests.Models.Generation;

using Microsoft.CodeAnalysis;

using SourceCodeRules.UsageAnalyzers;

namespace AIStudio.Tests.Analyzers;

/// <summary>
/// Checks that the texts of the app are looked up only through a T or TB wrapper, whose fallback text the I18N
/// collector finds.
/// </summary>
/// <remarks>
/// The collector reads the texts from calls of T and TB. A text handed to GetText anywhere else never reaches
/// the language plugins, so it is never translated, and nothing at runtime shows it. MWAIS0012 refuses such a
/// call while compiling. GetText is an extension method, so these tests ask in both of the forms C# knows for
/// one: the classic method with a this parameter and the member of an extension block.
/// </remarks>
[TestFixture]
public sealed class DirectI18NGetTextAnalyzerTests
{
    private const string CLASSIC_FORM =
        """
        public static class ILangExtensions
        {
            public static string GetText(this ILang lang, string fallbackEN) => fallbackEN;
        }
        """;

    private const string EXTENSION_BLOCK_FORM =
        """
        public static class ILangExtensions
        {
            extension(ILang lang)
            {
                public string GetText(string fallbackEN) => fallbackEN;
            }
        }
        """;

    [TestCase(CLASSIC_FORM, "T", TestName = "A T wrapper around a classic extension method")]
    [TestCase(CLASSIC_FORM, "TB", TestName = "A TB wrapper around a classic extension method")]
    [TestCase(EXTENSION_BLOCK_FORM, "T", TestName = "A T wrapper around the member of an extension block")]
    [TestCase(EXTENSION_BLOCK_FORM, "TB", TestName = "A TB wrapper around the member of an extension block")]
    public async Task AWrapperWhichForwardsItsTextIsAllowed(string form, string wrapper)
    {
        var reported = await AnalyzeAsync(form, $"public string {wrapper}(string fallbackEN) => this.GetText(fallbackEN);");

        Assert.That(reported, Is.Empty);
    }

    [TestCase(CLASSIC_FORM, """public string Describe() => this.GetText("Hello");""", TestName = "A classic extension method called from another method")]
    [TestCase(CLASSIC_FORM, """public string T(string fallbackEN) => this.GetText("Hello");""", TestName = "A classic extension method called by a wrapper with a text of its own")]
    [TestCase(CLASSIC_FORM, """public string Describe() => ILangExtensions.GetText(this, "Hello");""", TestName = "A classic extension method called as a static method")]
    [TestCase(EXTENSION_BLOCK_FORM, """public string Describe() => this.GetText("Hello");""", TestName = "The member of an extension block called from another method")]
    [TestCase(EXTENSION_BLOCK_FORM, """public string T(string fallbackEN) => this.GetText("Hello");""", TestName = "The member of an extension block called by a wrapper with a text of its own")]
    [TestCase(EXTENSION_BLOCK_FORM, """public string Describe() => ILangExtensions.GetText(this, "Hello");""", TestName = "The member of an extension block called as a static method")]
    public async Task ADirectLookupIsRefused(string form, string member)
    {
        var reported = await AnalyzeAsync(form, member);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(reported, Has.Count.EqualTo(1));
            Assert.That(reported.FirstOrDefault()?.Id, Is.EqualTo("MWAIS0012"));
        }
    }

    [TestCase("this.GetText(PLUGIN, fallbackEN)", 0, TestName = "The GetText of the app in a wrapper")]
    [TestCase("""this.GetText(PLUGIN, "Hello")""", 1, TestName = "The GetText of the app in a wrapper with a text of its own")]
    public async Task TheGetTextOfTheAppIsRecognized(string lookup, int expectedReports)
    {
        //
        // Whatever form the app gives its GetText, the analyzer has to know it:
        //
        var source =
            $$"""
              using AIStudio.Tools.PluginSystem;

              namespace Sample;

              public sealed class Component : ILang
              {
                  private static readonly ILanguagePlugin PLUGIN = NoPluginLanguage.INSTANCE;

                  public string T(string fallbackEN) => {{lookup}};

                  public string T(string fallbackEN, string? typeNamespace, string? typeName) => fallbackEN;
              }
              """;

        var reported = await CompilationHarness.AnalyzeAsync(source, new DirectI18NGetTextAnalyzer());

        Assert.That(reported, Has.Count.EqualTo(expectedReports));
    }

    private static async Task<IReadOnlyList<Diagnostic>> AnalyzeAsync(string form, string member)
    {
        //
        // The snippet declares its own ILang and ILangExtensions where the analyzer looks for them. They take
        // the place of those of the app, which the compilation references as well.
        //
        var source =
            $$"""
              namespace AIStudio.Tools.PluginSystem;

              public interface ILang
              {
              }

              {{form}}

              public sealed class Component : ILang
              {
                  {{member}}
              }
              """;

        return await CompilationHarness.AnalyzeAsync(source, new DirectI18NGetTextAnalyzer());
    }
}
