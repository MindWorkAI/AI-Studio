using System.Text;

using Lua;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks the Lua code which the I18N collector and the localization assistant write for the texts of the app.
/// </summary>
/// <remarks>
/// Both write each text as a comment followed by its assignment. A text with several lines, such as a notice in
/// Markdown, has to keep its line breaks, and its comment must not spill into the next line: Lua would read the rest
/// of the text as code there. Each case therefore runs the written code and compares what Lua reads with the text.
/// </remarks>
[TestFixture]
public sealed class LuaTableTests
{
    private const string KEY = "UI_TEXT_CONTENT.AISTUDIO.TOOLS.NOTICE.T123";
    private const string LUA_KEY = "AISTUDIO::TOOLS::NOTICE::T123";

    [TestCase("## Welcome!\n\nThe first paragraph.\nThe second line.", "## Welcome!\n\nThe first paragraph.\nThe second line.")]
    [TestCase("## Welcome!\r\n\r\nWindows line endings", "## Welcome!\n\nWindows line endings")]
    [TestCase("An old Mac line ending\rin between", "An old Mac line ending\nin between")]
    [TestCase("Quotes \"like these\",\na backslash \\,\nand -- no comment", "Quotes \"like these\",\na backslash \\,\nand -- no comment")]
    public async Task LuaReadsATextWithSeveralLinesWithItsLineBreaks(string text, string expected)
    {
        var code = Create(new Dictionary<string, string> { [KEY] = text });

        Assert.That(await ReadAsync(code, LUA_KEY), Is.EqualTo(expected), code);
    }

    [Test]
    public void TheCommentOfATextWithSeveralLinesStaysOnOneLine()
    {
        var code = Create(new Dictionary<string, string> { [KEY] = "## Welcome!\r\n\r\nThe first paragraph." });

        Assert.That(code.ReplaceLineEndings("\n").Split('\n'), Does.Contain("-- ## Welcome!  The first paragraph."), code);
    }

    [Test]
    public async Task TheCommentComesFromTheGivenContentAndStaysOnOneLine()
    {
        var comments = new Dictionary<string, string> { [KEY] = "## Welcome!\n\nThe first paragraph." };
        var code = Create(new Dictionary<string, string> { [KEY] = "## Willkommen!\n\nDer erste Absatz." }, comments);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(code.ReplaceLineEndings("\n").Split('\n'), Does.Contain("-- ## Welcome!  The first paragraph."), code);
            Assert.That(await ReadAsync(code, LUA_KEY), Is.EqualTo("## Willkommen!\n\nDer erste Absatz."), code);
        }
    }

    [Test]
    public async Task TheKeyOfAnInformalVariantKeepsItsEnding()
    {
        var code = Create(new Dictionary<string, string>
        {
            [KEY] = "Enter your name.",
            [SharedTools.I18NAddressVariant.InformalKeyOf(KEY)] = "Enter your name.",
        });

        Assert.That(await ReadAsync(code, $"{LUA_KEY}::INFORMAL"), Is.EqualTo("Enter your name."), code);
    }

    [Test]
    public void TextsOnOneLineAreWrittenAsBefore()
    {
        var code = Create(new Dictionary<string, string>
        {
            [KEY] = "Save \"this\" in C:\\Data ",
            ["UI_TEXT_CONTENT.AISTUDIO.TOOLS.NOTICE.T456"] = "Cancel"
        });

        Assert.That(code.ReplaceLineEndings("\n"), Is.EqualTo(
            """
            UI_TEXT_CONTENT = {}

            -- Save "this" in C:\Data
            UI_TEXT_CONTENT["AISTUDIO::TOOLS::NOTICE::T123"] = "Save \"this\" in C:\\Data"

            -- Cancel
            UI_TEXT_CONTENT["AISTUDIO::TOOLS::NOTICE::T456"] = "Cancel"

            """.ReplaceLineEndings("\n")));
    }

    /// <summary>
    /// The order of the texts must not depend on the region settings of the computer which writes them.
    /// </summary>
    /// <remarks>
    /// A comparison by culture sorts the underscore before the letters, the ordinal one after them. The
    /// language plugins were written in the first order before, so this test fails with it.
    /// </remarks>
    [Test]
    [SetCulture("de-DE")]
    public void TheTextsAreOrderedOrdinallyWhateverTheCulture()
    {
        var code = Create(new Dictionary<string, string>
        {
            ["UI_TEXT_CONTENT.AISTUDIO.DIALOGS.A_B.T1"] = "With an underscore",
            ["UI_TEXT_CONTENT.AISTUDIO.DIALOGS.AB.T1"] = "Without an underscore"
        });

        var withoutUnderscore = code.IndexOf("AISTUDIO::DIALOGS::AB::T1", StringComparison.Ordinal);
        var withUnderscore = code.IndexOf("AISTUDIO::DIALOGS::A_B::T1", StringComparison.Ordinal);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(withoutUnderscore, Is.GreaterThanOrEqualTo(0), code);
            Assert.That(withUnderscore, Is.GreaterThan(withoutUnderscore), code);
        }
    }

    private static string Create(IReadOnlyDictionary<string, string> texts, IReadOnlyDictionary<string, string>? comments = null)
    {
        // The Lua runtime has a type of the same name, which stands for a table in Lua:
        var builder = new StringBuilder();
        return SharedTools.LuaTable.Create(ref builder, "UI_TEXT_CONTENT", texts, comments);
    }

    private static async Task<string> ReadAsync(string code, string key)
    {
        using var state = LuaState.Create();
        await state.DoStringAsync(code);
        return state.Environment["UI_TEXT_CONTENT"].Read<LuaTable>()[key].Read<string>();
    }
}