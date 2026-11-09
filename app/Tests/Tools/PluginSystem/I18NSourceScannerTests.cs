using System.Runtime.CompilerServices;

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using SharedTools;

namespace AIStudio.Tests.Tools.PluginSystem;

/// <summary>
/// Checks how the I18N collector finds the texts of T(), TB(), and TA(), and which key it files them under.
/// </summary>
/// <remarks>
/// The app looks up a translation by the hash of the string the compiler made of a text. The collector has to file
/// each text under the same hash, or the text stays English in every language without any notice. The checks over
/// the source of the app therefore compare every text the collector finds with what the compiler makes of it.
/// </remarks>
[TestFixture]
public sealed class I18NSourceScannerTests
{
    [Test]
    public void EveryTextOfTheAppIsReadAsTheCompilerReadsIt()
    {
        var texts = 0;
        var mismatches = new List<string>();
        foreach (var file in SourceFiles())
        {
            var content = File.ReadAllText(file);
            var warnings = new List<string>();
            foreach (var text in I18NSourceScanner.FindTexts(content, file, warnings))
            {
                texts++;
                var token = SyntaxFactory.ParseToken(content, text.Offset);
                if (token.ContainsDiagnostics || token.HasLeadingTrivia || token.Span.Length != text.Length || token.ValueText != text.Text)
                    mismatches.Add($"{Path.GetRelativePath(AppSourceDirectory(), file)} at {text.Offset}: '{text.Text}' instead of '{token.ValueText}'");
            }
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(texts, Is.GreaterThan(5_000), "The test found too few texts to be looking at the source of the app.");
            Assert.That(mismatches, Is.Empty);
        }
    }

    [Test]
    public void EveryCallWithATextIsFound()
    {
        var missing = new List<string>();
        var calls = 0;
        foreach (var file in SourceFiles().Where(file => file.EndsWith(".cs", StringComparison.Ordinal)))
        {
            var content = File.ReadAllText(file);
            if (!content.Contains("T(", StringComparison.Ordinal))
                continue;

            var found = I18NSourceScanner.FindTexts(content, file, []).Select(text => text.Offset).ToHashSet();
            var root = CSharpSyntaxTree.ParseText(content).GetRoot();
            foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (CalledName(invocation) is not ("T" or "TB" or "TA") ||invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression is not LiteralExpressionSyntax literal)
                    continue;

                // The collector takes only a text which follows the parenthesis right away:
                if (literal.SpanStart != invocation.ArgumentList.OpenParenToken.Span.End)
                    continue;

                calls++;
                if (!found.Contains(literal.SpanStart))
                    missing.Add($"{Path.GetRelativePath(AppSourceDirectory(), file)}: {literal}");
            }
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(calls, Is.GreaterThan(2_000), "The test found too few calls to be looking at the source of the app.");
            Assert.That(missing, Is.Empty);
        }
    }

    [Test]
    public void TheSourceOfTheAppCausesNoWarning()
    {
        var warnings = new List<string>();
        foreach (var file in SourceFiles())
            I18NSourceScanner.FindTexts(File.ReadAllText(file), Path.GetRelativePath(AppSourceDirectory(), file), warnings);

        Assert.That(warnings, Is.Empty);
    }

    [Test]
    public void AMultiLineTextIsFoundWithoutItsIndentation()
    {
        const string SOURCE = """"
                              public string Notice => TB("""
                                  ## Welcome!

                                  The first paragraph.
                                  """);
                              """";

        var texts = I18NSourceScanner.FindTexts(SOURCE, "Notice.cs", []);

        Assert.That(texts.Select(text => text.Text), Is.EqualTo(new[] { "## Welcome!\n\nThe first paragraph." }));
    }

    [Test]
    public void AllKindsOfCallsAreFound()
    {
        const string SOURCE = """
                              T("First"); TB("Second"); this.T("Third", typeof(X).Namespace, nameof(X)); @T(@"Fourth"); TA("Fifth"); @TA("Sixth"); T( "Not found");
                              """;

        var texts = I18NSourceScanner.FindTexts(SOURCE, "Calls.cs", []);

        Assert.That(texts.Select(text => text.Text), Is.EqualTo(new[] { "First", "Second", "Third", "Fourth", "Fifth", "Sixth" }));
    }

    [Test]
    public void OnlyATextOfTAGetsAVariantWithTheInformalAddress()
    {
        const string SOURCE = """T("Save"); TB("Your details"); TA("Enter your name."); TA(@"Please check ""this"".");""";

        var texts = I18NSourceScanner.FindTexts(SOURCE, "Address.cs", []);

        Assert.That(texts.Where(text => text.HasAddressVariants).Select(text => text.Text), Is.EqualTo(new[] { "Enter your name.", "Please check \"this\"." }));
    }

    [Test]
    public void ACallOfAnotherMethodIsIgnored()
    {
        const string SOURCE = """GetT("No text"); MyTB("No text either"); DATA("No text at all"); T("Text");""";

        var texts = I18NSourceScanner.FindTexts(SOURCE, "Other.cs", []);

        Assert.That(texts.Select(text => text.Text), Is.EqualTo(new[] { "Text" }));
    }

    [TestCase("""T($"Hello {name}");""")]
    [TestCase("""T("Hello " + name);""")]
    [TestCase("""T("Unterminated);""")]
    public void ATextWhichCannotBeTranslatedCausesAWarning(string source)
    {
        var warnings = new List<string>();

        var texts = I18NSourceScanner.FindTexts(source, "Warning.cs", warnings);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(texts, Is.Empty);
            Assert.That(warnings, Has.Count.EqualTo(1));
        }
    }

    [Test]
    public void LineEndingsDoNotChangeTheKey()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(I18NKeyHash.Of("First line\r\nsecond line"), Is.EqualTo(I18NKeyHash.Of("First line\nsecond line")));
            Assert.That(I18NKeyHash.Of("First line\rsecond line"), Is.EqualTo(I18NKeyHash.Of("First line\nsecond line")));
        }
    }

    [TestCase("Save")]
    [TestCase("Parameter: ")]
    [TestCase("""Invalid JSON: Add the parameters in proper JSON formatting, e.g., "temperature": 0.5.""")]
    public void ATextWithoutLineBreaksKeepsItsKey(string text)
    {
        Assert.That(I18NKeyHash.Of(text), Is.EqualTo(text.ToFNV32()));
    }

    private static string? CalledName(InvocationExpressionSyntax invocation) => invocation.Expression switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.Text,
        MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.Text,
        _ => null,
    };

    private static IEnumerable<string> SourceFiles()
    {
        var appDirectory = AppSourceDirectory();
        return Directory.EnumerateFiles(appDirectory, "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cs", StringComparison.Ordinal) || path.EndsWith(".razor", StringComparison.Ordinal))
            .Where(path => Path.GetRelativePath(appDirectory, path).Split(Path.DirectorySeparatorChar)[0] is not ("bin" or "obj" or "wwwroot"));
    }

    private static string AppSourceDirectory([CallerFilePath] string sourceFilePath = "") => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourceFilePath)!, "..", "..", "..", "MindWork AI Studio"));
}