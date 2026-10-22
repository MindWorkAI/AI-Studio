using AIStudio.Tools.PluginSystem;

using Lua;

using SharedTools;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks that a text written as a Lua string literal reads back as the very same text.
/// </summary>
/// <remarks>
/// Profiles, chat templates, and document analysis policies are exported through these literals. Since
/// an export can be imported, it is read back on other computers as well: by Lua in a configuration
/// plugin, and by the snippet parser in an import. Long strings keep long texts readable, but Lua reads
/// them by rules of its own: it drops a line break right after the opening bracket, turns every kind of
/// line break into \n, and ends the string at the first closing bracket of its level. Each case is
/// therefore read both ways and compared with the original.
/// </remarks>
[TestFixture]
public sealed class LuaToolsTests
{
    private static readonly string LONG_TEXT = string.Concat(Enumerable.Repeat("A text longer than what a quoted string takes. ", 3));

    private static IEnumerable<TestCaseData> Texts()
    {
        foreach (var forceLongString in new[] { false, true })
        {
            var suffix = forceLongString ? ", forced long string" : string.Empty;
            yield return new TestCaseData(string.Empty, forceLongString).SetArgDisplayNames($"An empty text{suffix}");
            yield return new TestCaseData("Short, with \"quotes\" and C:\\Users\\me", forceLongString).SetArgDisplayNames($"Quotes and backslashes{suffix}");
            yield return new TestCaseData(LONG_TEXT, forceLongString).SetArgDisplayNames($"A long text{suffix}");
            yield return new TestCaseData("First line\nSecond line", forceLongString).SetArgDisplayNames($"A line break in between{suffix}");
            yield return new TestCaseData("\nStarts with a line break", forceLongString).SetArgDisplayNames($"A line break at the start{suffix}");
            yield return new TestCaseData("\n\nStarts with an empty line", forceLongString).SetArgDisplayNames($"An empty line at the start{suffix}");
            yield return new TestCaseData("Windows line breaks\r\nin between\r\n", forceLongString).SetArgDisplayNames($"Windows line breaks{suffix}");
            yield return new TestCaseData("An old Mac line break\rin between", forceLongString).SetArgDisplayNames($"A carriage return alone{suffix}");
            yield return new TestCaseData("Holds ]] and ]===] in between", forceLongString).SetArgDisplayNames($"Closing brackets in between{suffix}");
            yield return new TestCaseData("Ends like a closing bracket ]===", forceLongString).SetArgDisplayNames($"The start of a closing bracket at the end{suffix}");
            yield return new TestCaseData("Holds ]===] and ends like the next level ]====", forceLongString).SetArgDisplayNames($"The start of the next level at the end{suffix}");
        }
    }

    [TestCaseSource(nameof(Texts))]
    public async Task LuaReadsTheLiteralAsTheOriginalText(string text, bool forceLongString)
    {
        var literal = LuaTools.ToLuaStringLiteral(text, forceLongString);
        var state = LuaState.Create();
        await state.DoStringAsync($"value = {literal}");

        Assert.That(state.Environment["value"].Read<string>(), Is.EqualTo(text), literal);
    }

    [TestCaseSource(nameof(Texts))]
    public void AnImportReadsTheLiteralAsTheOriginalText(string text, bool forceLongString)
    {
        var snippet = $$"""CONFIG["PROFILES"][#CONFIG["PROFILES"]+1] = { ["Text"] = {{LuaTools.ToLuaStringLiteral(text, forceLongString)}} }""";

        Assert.That(ConfigurationSnippetParser.TryParse(snippet, PluginConfigurationObjectType.PROFILE, out var table, out var issue), Is.True, issue);
        Assert.That(table["Text"].Read<string>(), Is.EqualTo(text), snippet);
    }
}