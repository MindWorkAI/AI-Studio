using AIStudio.Settings;
using AIStudio.Tools;

using Lua;
using Lua.Standard;

namespace AIStudio.Tests.Settings;

/// <summary>
/// Checks how a glossary of a configuration plugin is read from its Lua table.
/// </summary>
/// <remarks>
/// An organization ships a glossary to everybody, so a mistake in the table must not turn into a
/// glossary which translates into the wrong language or quietly lacks its entries. A table without
/// what a glossary needs is refused as a whole; a single entry without a term or a value is left out,
/// so one typo does not cost the whole glossary.
/// </remarks>
[TestFixture]
public sealed class GlossaryConfigurationTests
{
    private static readonly Guid PLUGIN_ID = new("22222222-2222-2222-2222-222222222222");

    private const string GLOSSARY_ID = "11111111-1111-1111-1111-111111111111";

    private static readonly string PLUGIN_PATH = Path.Combine(Path.GetTempPath(), "ai-studio-glossary-plugin");

    [Test]
    public async Task AFileWithinThePluginIsReadWithItsColumns()
    {
        var glossary = await ParseAsync($$"""
                                          {
                                              ["Id"] = "{{GLOSSARY_ID}}",
                                              ["Name"] = "HR terms",
                                              ["Type"] = "REFERENCE",
                                              ["File"] = { ["Path"] = "glossaries/hr.xlsx", ["TermColumn"] = "Begriff", ["ValueColumn"] = "Erklärung", ["NoteColumn"] = "Notiz" },
                                              ["Entries"] = { { ["Term"] = "ignored", ["Value"] = "because of the file" } },
                                          }
                                          """);

        var file = glossary!.PluginSource!.File;
        Assert.Multiple(() =>
        {
            Assert.That(file, Is.Not.Null);
            Assert.That(file!.FullPath, Is.EqualTo(Path.GetFullPath(Path.Combine(PLUGIN_PATH, "glossaries", "hr.xlsx"))));
            Assert.That(file.TermColumn, Is.EqualTo("Begriff"));
            Assert.That(file.ValueColumn, Is.EqualTo("Erklärung"));
            Assert.That(file.OtherFormsColumn, Is.Null);
            Assert.That(file.NoteColumn, Is.EqualTo("Notiz"));
            Assert.That(glossary.PluginSource.Entries, Is.Empty, "The entries of the file count, not the listed ones.");
        });
    }

    [TestCase("../outside.xlsx", TestName = "A path leaving the plugin folder is refused")]
    [TestCase("glossaries/../../outside.xlsx", TestName = "A path leaving the plugin folder on its way is refused")]
    [TestCase("/etc/glossary.csv", TestName = "An absolute path is refused")]
    [TestCase("", TestName = "An empty path is refused")]
    public async Task AFileOutsideOfThePluginIsRefused(string path)
    {
        var glossary = await ParseAsync($$"""
                                          {
                                              ["Id"] = "{{GLOSSARY_ID}}",
                                              ["Name"] = "HR terms",
                                              ["Type"] = "REFERENCE",
                                              ["File"] = { ["Path"] = "{{path}}", ["TermColumn"] = "Begriff", ["ValueColumn"] = "Erklärung" },
                                          }
                                          """);

        Assert.That(glossary, Is.Null);
    }

    [Test]
    public async Task AFileWithoutItsColumnsIsRefused()
    {
        var glossary = await ParseAsync($$"""
                                          {
                                              ["Id"] = "{{GLOSSARY_ID}}",
                                              ["Name"] = "HR terms",
                                              ["Type"] = "REFERENCE",
                                              ["File"] = { ["Path"] = "glossaries/hr.xlsx", ["TermColumn"] = "Begriff" },
                                          }
                                          """);

        Assert.That(glossary, Is.Null);
    }

    [Test]
    public void AWindowsAbsolutePathIsRefusedOnWindows()
    {
        if (!OperatingSystem.IsWindows())
            Assert.Ignore("Only Windows reads a drive letter as the root of a path.");

        Assert.That(Glossary.TryResolvePluginFile(@"C:\Windows\glossary.csv", PLUGIN_PATH, out _), Is.False);
    }

    [Test]
    public async Task ATranslationGlossaryIsReadWithItsEntries()
    {
        var glossary = await ParseAsync($$"""
                                          {
                                              ["Id"] = "{{GLOSSARY_ID}}",
                                              ["Name"] = "HR terms",
                                              ["Type"] = "TRANSLATION",
                                              ["Description"] = "Terms of our HR department.",
                                              ["SourceLanguage"] = "DE_DE",
                                              ["TargetLanguage"] = "en_us",
                                              ["PreselectInTranslation"] = false,
                                              ["Entries"] = {
                                                  { ["Term"] = "Arbeitszeit", ["Value"] = "working time", ["OtherForms"] = { "Arbeitszeiten" }, ["Note"] = "HR only" },
                                                  { ["Term"] = "Urlaub", ["Value"] = "vacation" },
                                              },
                                          }
                                          """);

        Assert.That(glossary, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(glossary!.Id, Is.EqualTo(GLOSSARY_ID));
            Assert.That(glossary.Type, Is.EqualTo(GlossaryType.TRANSLATION));
            Assert.That(glossary.SourceLanguage, Is.EqualTo(CommonLanguages.DE_DE));
            Assert.That(glossary.TargetLanguage, Is.EqualTo(CommonLanguages.EN_US), "The case of a language must not matter.");
            Assert.That(glossary.PreselectInTranslation, Is.False);
            Assert.That(glossary.IsEnterpriseConfiguration, Is.True);
            Assert.That(glossary.EnterpriseConfigurationPluginId, Is.EqualTo(PLUGIN_ID));

            var entries = glossary.PluginSource!.Entries;
            Assert.That(entries.Select(entry => entry.Term), Is.EqualTo(new[] { "Arbeitszeit", "Urlaub" }));
            Assert.That(entries[0].Aliases, Is.EqualTo(new[] { "Arbeitszeiten" }));
            Assert.That(entries[0].Note, Is.EqualTo("HR only"));
            Assert.That(entries[1].Aliases, Is.Null);
        });
    }

    [Test]
    public async Task AReferenceGlossaryNeedsNoLanguagesAndIsPreselectedByDefault()
    {
        var glossary = await ParseAsync($$"""
                                          {
                                              ["Id"] = "{{GLOSSARY_ID}}",
                                              ["Name"] = "Harry Potter",
                                              ["Type"] = "REFERENCE",
                                              ["Entries"] = { { ["Term"] = "Muggel", ["Value"] = "A person without magical abilities." } },
                                          }
                                          """);

        Assert.Multiple(() =>
        {
            Assert.That(glossary!.Type, Is.EqualTo(GlossaryType.REFERENCE));
            Assert.That(glossary.SourceLanguage, Is.EqualTo(CommonLanguages.AS_IS));
            Assert.That(glossary.PreselectInTranslation, Is.True);
        });
    }

    [Test]
    public async Task AnEntryWithoutATermOrAValueIsLeftOut()
    {
        var glossary = await ParseAsync($$"""
                                          {
                                              ["Id"] = "{{GLOSSARY_ID}}",
                                              ["Name"] = "Harry Potter",
                                              ["Type"] = "REFERENCE",
                                              ["Entries"] = {
                                                  { ["Term"] = "Muggel", ["Value"] = "" },
                                                  { ["Value"] = "orphan" },
                                                  { ["Term"] = "Quidditch", ["Value"] = "A game on broomsticks." },
                                              },
                                          }
                                          """);

        Assert.That(glossary!.PluginSource!.Entries.Select(entry => entry.Term), Is.EqualTo(new[] { "Quidditch" }));
    }

    [TestCase("""["Name"] = "HR terms", ["Type"] = "TRANSLATION", ["SourceLanguage"] = "DE_DE", ["TargetLanguage"] = "EN_US" """, TestName = "A glossary without an ID is refused")]
    [TestCase($"""["Id"] = "{GLOSSARY_ID}", ["Type"] = "REFERENCE" """, TestName = "A glossary without a name is refused")]
    [TestCase($"""["Id"] = "{GLOSSARY_ID}", ["Name"] = "HR terms", ["Type"] = "DICTIONARY" """, TestName = "A glossary of an unknown type is refused")]
    [TestCase($"""["Id"] = "{GLOSSARY_ID}", ["Name"] = "HR terms", ["Type"] = "1" """, TestName = "A type given as a number is refused")]
    [TestCase($"""["Id"] = "{GLOSSARY_ID}", ["Name"] = "HR terms", ["Type"] = "TRANSLATION", ["SourceLanguage"] = "DE_DE" """, TestName = "A translation glossary without a target language is refused")]
    [TestCase($"""["Id"] = "{GLOSSARY_ID}", ["Name"] = "HR terms", ["Type"] = "TRANSLATION", ["SourceLanguage"] = "AS_IS", ["TargetLanguage"] = "EN_US" """, TestName = "A translation glossary without a real source language is refused")]
    [TestCase($"""["Id"] = "{GLOSSARY_ID}", ["Name"] = "HR terms", ["Type"] = "TRANSLATION", ["SourceLanguage"] = "DE_DE", ["TargetLanguage"] = "OTHER" """, TestName = "A translation glossary into another language has to name it")]
    public async Task AnIncompleteGlossaryIsRefused(string fields)
    {
        var glossary = await ParseAsync($"{{ {fields} }}");

        Assert.That(glossary, Is.Null);
    }

    [Test]
    public async Task AnotherLanguageIsReadWithItsName()
    {
        var glossary = await ParseAsync($$"""
                                          {
                                              ["Id"] = "{{GLOSSARY_ID}}",
                                              ["Name"] = "Fantasy",
                                              ["Type"] = "TRANSLATION",
                                              ["SourceLanguage"] = "EN_US",
                                              ["TargetLanguage"] = "OTHER",
                                              ["CustomTargetLanguage"] = " Klingon ",
                                          }
                                          """);

        Assert.Multiple(() =>
        {
            Assert.That(glossary!.TargetLanguage, Is.EqualTo(CommonLanguages.OTHER));
            Assert.That(glossary.CustomTargetLanguage, Is.EqualTo("Klingon"));
            Assert.That(glossary.PluginSource!.Entries, Is.Empty, "A glossary without entries is still a glossary; the log says so.");
        });
    }

    /// <summary>
    /// Reads a glossary table the way a configuration plugin states it.
    /// </summary>
    /// <returns>The glossary, or null when the table was refused.</returns>
    private static async Task<Glossary?> ParseAsync(string luaTable)
    {
        var state = LuaState.Create();
        state.OpenBasicLibrary();
        await state.DoStringAsync($"GLOSSARY = {luaTable}");

        if (!state.Environment["GLOSSARY"].TryRead<LuaTable>(out var table))
            throw new InvalidOperationException("The test table could not be read.");

        return Glossary.TryParseGlossaryTable(1, table, PLUGIN_ID, PLUGIN_PATH, out var glossary) ? (Glossary)glossary : null;
    }
}