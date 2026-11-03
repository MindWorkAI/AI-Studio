using AIStudio.Tools;
using AIStudio.Tools.CodeHighlighting;

namespace AIStudio.Tests.Tools.CodeHighlighting;

/// <summary>
/// Checks which languages AI Studio highlights itself, judged by what a model writes behind the opening
/// fence of a code block.
/// </summary>
[TestFixture]
public sealed class CodeHighlightersTests
{
    [TestCase("python")]
    [TestCase("py")]
    [TestCase("Python3")]
    [TestCase("bash")]
    [TestCase("sh")]
    [TestCase("pwsh")]
    [TestCase("lua")]
    [TestCase("js")]
    [TestCase("node")]
    [TestCase("jsx")]
    [TestCase("ts")]
    [TestCase("tsx")]
    [TestCase("css")]
    public void AnAliasFindsItsLanguage(string fenceLanguage)
    {
        Assert.That(CodeHighlighters.TryGetRules(fenceLanguage, out _), Is.True);
    }

    [TestCase("csharp", Description = "MudBlazor.Markdown highlights it itself.")]
    [TestCase("foo", Description = "A language nobody knows stays plain.")]
    [TestCase("text")]
    [TestCase("")]
    [TestCase(null)]
    public void OtherLanguagesAreLeftToTheLibrary(string? fenceLanguage)
    {
        Assert.That(CodeHighlighters.TryGetRules(fenceLanguage, out _), Is.False);
    }

    /// <summary>
    /// A language is found by the name SourceCodeLanguages gives it. A name it does not know would make
    /// the rules unreachable, without any error.
    /// </summary>
    [Test]
    public void EveryLanguageIsOneSourceCodeLanguagesKnows()
    {
        var knownNames = SourceCodeLanguages.Languages.Select(language => language.Name).ToHashSet(StringComparer.Ordinal);
        Assert.That(CodeHighlighters.LanguageNames, Is.SubsetOf(knownNames));
    }

    /// <summary>
    /// A model writes anything, and a streamed answer stops anywhere. Whatever the code, the parts stay
    /// in order, inside the code, and never overlap.
    /// </summary>
    [Test]
    public void AnyCodeGivesPartsInOrder([ValueSource(nameof(LanguageNames))] string languageName)
    {
        const string CHARACTERS = "\"'`\\/*#$@{}()[]=-.:;!%<>abcdefLrXYZ019_ \n\r\t";
        Assert.That(CodeHighlighters.TryGetRules(languageName, out var rules), Is.True, "The name of a language is one of its aliases, too.");

        var random = new Random(languageName.Length);
        for (var sample = 0; sample < 500; sample++)
        {
            var code = new string(Enumerable.Range(0, random.Next(1, 160)).Select(_ => CHARACTERS[random.Next(CHARACTERS.Length)]).ToArray());
            var end = 0;
            foreach (var token in CodeTokenizer.Tokenize(code, rules!))
            {
                Assert.That(token.Start, Is.GreaterThanOrEqualTo(end), $"A part overlaps the one before it in: {code}");
                Assert.That(token.Length, Is.GreaterThan(0), $"A part is empty in: {code}");
                end = token.Start + token.Length;
                Assert.That(end, Is.LessThanOrEqualTo(code.Length), $"A part runs past the end of: {code}");
            }
        }
    }

    private static IEnumerable<string> LanguageNames() => CodeHighlighters.LanguageNames;
}