using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

using Lua;

using SharedTools;

namespace AIStudio.Tests.Tools.PluginSystem;

/// <summary>
/// Checks that every text which addresses the reader follows the form of address the person chose, and that the
/// German translation keeps both forms apart.
/// </summary>
/// <remarks>
/// A text which addresses the reader needs TA(); with T() or TB(), it would stay formal in German, although the
/// person chose the informal address. English shows such a text by "you". German also shows a request without
/// "you", such as "Please select a provider.", which becomes "Bitte wählen Sie einen Anbieter.": a capitalized
/// "Sie", "Ihr", or "Ihnen" inside a sentence always addresses the reader. At the start of a sentence, it may
/// mean "they" or "its" as well, so the tests leave that out, and also a pronoun in parentheses or quotation
/// marks, which names the form of address instead of using it. A thank-you on the supporters page addresses the
/// supporter, not the reader: it stays with T(), and its German says "du" whatever the reader chose. US English
/// addresses everybody alike, so its informal variants read like its formal texts, and it offers no choice.
/// </remarks>
[TestFixture]
public sealed partial class AddressedTextsTests
{
    private const string GERMAN_PLUGIN_DIRECTORY = "de-de-43065dbc-78d0-45b7-92be-f14c2926e2dc";
    private const string ENGLISH_PLUGIN_DIRECTORY = "en-us-97dfb1ba-50c4-4440-8dfa-6575daf543c8";

    /// <summary>
    /// What stands right before the literal of a thank-you to a supporter, see "Crediting contributors" in app/Build/AGENTS.md.
    /// </summary>
    private const string ACKNOWLEDGMENT_CALL = "Acknowledgment=\"@T(";

    [Test]
    public void EveryTextWhichSaysYouUsesTA()
    {
        var appDirectory = AppSourceDirectory();
        var texts = 0;
        var formalOnly = new List<string>();
        foreach (var file in SourceFiles(appDirectory))
        {
            var content = File.ReadAllText(file);
            foreach (var text in I18NSourceScanner.FindTexts(content, file, []))
            {
                texts++;
                if (!text.HasAddressVariants && EnglishAddressRegex().IsMatch(text.Text) && !IsAcknowledgment(content, text))
                    formalOnly.Add($"{Path.GetRelativePath(appDirectory, file)}: {text.Text}");
            }
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(texts, Is.GreaterThan(5000), "The test found too few texts to be looking at the source of the app.");
            Assert.That(formalOnly, Is.Empty, "These texts address the reader; use TA() instead of T() or TB().");
        }
    }

    [Test]
    public async Task EveryTextWhichGermanTranslatesWithSieUsesTA()
    {
        var collected = await ReadTextsAsync(Path.Combine(AppSourceDirectory(), "Assistants", "I18N", "allTexts.lua"));
        var german = await ReadGermanTextsAsync();
        var formalOnly = german
            .Where(entry => collected.ContainsKey(entry.Key) && !I18NAddressVariant.TryGetFormalKey(entry.Key, out _))
            .Where(entry => !collected.ContainsKey(I18NAddressVariant.InformalKeyOf(entry.Key)) && AddressesFormally(entry.Value))
            .Select(entry => $"{entry.Key}: {collected[entry.Key]}")
            .ToList();

        Assert.That(formalOnly, Is.Empty, "The German translation of these texts addresses the reader; use TA() instead of T() or TB(), and run the I18N collector.");
    }

    [Test]
    public async Task NoFormalGermanTextAddressesTheReaderInformally()
    {
        var collected = await ReadTextsAsync(Path.Combine(AppSourceDirectory(), "Assistants", "I18N", "allTexts.lua"));
        var acknowledgments = AcknowledgmentTexts();
        var informal = (await ReadGermanTextsAsync())
            .Where(entry => !I18NAddressVariant.TryGetFormalKey(entry.Key, out _) && AddressesInformally(entry.Value))
            .Where(entry => !collected.TryGetValue(entry.Key, out var english) || !acknowledgments.Contains(english))
            .Select(entry => $"{entry.Key}: {entry.Value}")
            .ToList();

        Assert.That(informal, Is.Empty, "These formal translations say \"du\"; the formal address of the app is \"Sie\".");
    }

    [Test]
    public async Task NoInformalGermanTextAddressesTheReaderFormally()
    {
        var formal = (await ReadGermanTextsAsync())
            .Where(entry => I18NAddressVariant.TryGetFormalKey(entry.Key, out _) && AddressesFormally(entry.Value))
            .Select(entry => $"{entry.Key}: {entry.Value}")
            .ToList();

        Assert.That(formal, Is.Empty, "These informal variants say \"Sie\"; an informal variant says \"du\".");
    }

    [Test]
    public async Task EnglishOffersNoChoiceOfAddress()
    {
        var english = await ReadTextsAsync(Path.Combine(AppSourceDirectory(), "Plugins", "languages", ENGLISH_PLUGIN_DIRECTORY, "plugin.lua"));
        var differing = english
            .Where(entry => I18NAddressVariant.TryGetFormalKey(entry.Key, out var formalKey) && english.TryGetValue(formalKey, out var formalText) && !string.Equals(formalText, entry.Value, StringComparison.Ordinal))
            .Select(entry => $"{entry.Key}: {entry.Value}")
            .ToList();

        Assert.That(differing, Is.Empty, "These informal variants differ from their formal texts, so that US English would offer the choice of address; run the localization assistant for US English.");
    }

    [TestCase("Bitte wählen Sie einen Anbieter.", true)]
    [TestCase("Das Lab nutzt Ihre Angaben.", true)]
    [TestCase("Sie läuft ohne Konfiguration.", false)]
    [TestCase("Die Datei ist keine Textdatei. Ihr Inhalt fehlt.", false)]
    [TestCase("Formelle Anrede (Sie)", false)]
    [TestCase("Die Anrede „Sie“ gilt überall.", false)]
    public void TheGermanFormalAddressIsFoundInsideASentenceOnly(string text, bool addressesFormally) => Assert.That(AddressesFormally(text), Is.EqualTo(addressesFormally));

    /// <remarks>
    /// An imperative such as "wähle" says "du" without the pronoun, so only the pronouns tell.
    /// </remarks>
    [TestCase("Wähle deinen Anbieter.", true)]
    [TestCase("Du hast noch keine Vorbereitung.", true)]
    [TestCase("Informelle Anrede (du)", false)]
    [TestCase("Bitte wählen Sie einen Anbieter.", false)]
    public void TheGermanInformalAddressIsFoundByItsPronouns(string text, bool addressesInformally) => Assert.That(AddressesInformally(text), Is.EqualTo(addressesInformally));

    /// <summary>
    /// Whether a German text addresses the reader with "Sie", "Ihr", or "Ihnen" inside a sentence.
    /// </summary>
    private static bool AddressesFormally(string text) => FindsAddress(text, GermanFormalRegex());

    /// <summary>
    /// Whether a German text addresses the reader with "du", "dich", "dir", or "dein".
    /// </summary>
    private static bool AddressesInformally(string text)
    {
        foreach (Match match in GermanInformalRegex().Matches(text))
        {
            if (!IsNamedOnly(text, match))
                return true;
        }

        return false;
    }

    private static bool FindsAddress(string text, Regex pronouns)
    {
        foreach (Match match in pronouns.Matches(text))
        {
            var before = text[..match.Index].TrimEnd(' ', '\t');
            if (before.Length == 0 || before[^1] is '.' or '!' or '?' or ':' or '\n' or '#' or '*' or '-')
                continue;

            if (IsNamedOnly(text, match))
                continue;

            return true;
        }

        return false;
    }

    /// <summary>
    /// Whether the pronoun stands in parentheses or quotation marks, i.e., names a form of address instead of using it.
    /// </summary>
    private static bool IsNamedOnly(string text, Match match)
    {
        var before = match.Index > 0 ? text[match.Index - 1] : ' ';
        var after = match.Index + match.Length < text.Length ? text[match.Index + match.Length] : ' ';
        return before is '(' or '„' or '"' || after is ')' or '“' or '"';
    }

    /// <summary>
    /// Whether the text thanks a supporter on the supporters page.
    /// </summary>
    private static bool IsAcknowledgment(string content, I18NSourceText text) => content.AsSpan(0, text.Offset).EndsWith(ACKNOWLEDGMENT_CALL, StringComparison.Ordinal);

    /// <summary>
    /// The English texts which thank a supporter, with LF line endings like the collected texts.
    /// </summary>
    private static HashSet<string> AcknowledgmentTexts()
    {
        var acknowledgments = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in SourceFiles(AppSourceDirectory()))
        {
            var content = File.ReadAllText(file);
            if (!content.Contains(ACKNOWLEDGMENT_CALL, StringComparison.Ordinal))
                continue;

            foreach (var text in I18NSourceScanner.FindTexts(content, file, []).Where(text => IsAcknowledgment(content, text)))
                acknowledgments.Add(I18NKeyHash.NormalizeLineEndings(text.Text));
        }

        return acknowledgments;
    }

    private static async Task<Dictionary<string, string>> ReadGermanTextsAsync() => await ReadTextsAsync(Path.Combine(AppSourceDirectory(), "Plugins", "languages", GERMAN_PLUGIN_DIRECTORY, "plugin.lua"));

    /// <summary>
    /// Reads the table UI_TEXT_CONTENT of a language file, by its keys.
    /// </summary>
    /// <remarks>
    /// The language plugins load their icon with require, which this test needs not.
    /// </remarks>
    private static async Task<Dictionary<string, string>> ReadTextsAsync(string path)
    {
        var state = LuaState.Create();
        await state.DoStringAsync($"function require(_) end\n{await File.ReadAllTextAsync(path)}");
        if (!state.Environment["UI_TEXT_CONTENT"].TryRead<Lua.LuaTable>(out var table))
            throw new InvalidOperationException($"The file '{path}' has no table UI_TEXT_CONTENT.");

        var texts = new Dictionary<string, string>(StringComparer.Ordinal);
        var lastKey = LuaValue.Nil;
        while (table.TryGetNext(lastKey, out var pair))
        {
            if (pair.Value.TryRead<string>(out var value))
                texts[pair.Key.ToString()] = value;

            lastKey = pair.Key;
        }

        return texts;
    }

    private static IEnumerable<string> SourceFiles(string appDirectory) => Directory.EnumerateFiles(appDirectory, "*.*", SearchOption.AllDirectories)
        .Where(path => path.EndsWith(".cs", StringComparison.Ordinal) || path.EndsWith(".razor", StringComparison.Ordinal))
        .Where(path => Path.GetRelativePath(appDirectory, path).Split(Path.DirectorySeparatorChar)[0] is not ("bin" or "obj" or "wwwroot"));

    [GeneratedRegex(@"\b(you|your|yours|yourself|yourselves)\b", RegexOptions.IgnoreCase)]
    private static partial Regex EnglishAddressRegex();

    [GeneratedRegex(@"\b(Sie|Ihr|Ihre|Ihren|Ihrem|Ihrer|Ihres|Ihnen)\b")]
    private static partial Regex GermanFormalRegex();

    [GeneratedRegex(@"\b([Dd]u|[Dd]ich|[Dd]ir|[Dd]ein|[Dd]eine|[Dd]einen|[Dd]einem|[Dd]einer|[Dd]eines)\b")]
    private static partial Regex GermanInformalRegex();

    private static string AppSourceDirectory([CallerFilePath] string sourceFilePath = "") => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourceFilePath)!, "..", "..", "..", "MindWork AI Studio"));
}