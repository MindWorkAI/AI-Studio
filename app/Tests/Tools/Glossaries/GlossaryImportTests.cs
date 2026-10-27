using AIStudio.Tools;
using AIStudio.Tools.Glossaries;

namespace AIStudio.Tests.Tools.Glossaries;

/// <summary>
/// Checks how the rows of a table become entries of a glossary.
/// </summary>
/// <remarks>
/// Users import the same spreadsheet again after they changed it, so an entry with a known term has
/// to be replaced rather than doubled. Rows without a term or a value would be useless in the glossary,
/// and the user should hear how many were left out instead of finding them missing later.
/// </remarks>
[TestFixture]
public sealed class GlossaryImportTests
{
    private static readonly GlossaryImportTable TABLE = new(
        ["Deutsch", "English", "Varianten", "Notiz"],
        [
            ["Arbeitszeit", "working time", "", ""],
            ["Muggel", "muggle", "Muggels; Muggeln", "Harry Potter"],
            ["", "orphan value", "", ""],
            ["Urlaub", "", "", ""],
        ]);

    private static readonly GlossaryColumnMapping MAPPING = new(TermColumn: 0, ValueColumn: 1, AliasesColumn: 2, NoteColumn: 3);

    [Test]
    public void RowsBecomeEntriesWithTheirAliasesAndNotes()
    {
        var result = GlossaryImport.Apply([], TABLE, MAPPING, GlossaryImportMode.MERGE);

        Assert.Multiple(() =>
        {
            Assert.That(result.Entries.Select(entry => entry.Term), Is.EqualTo(new[] { "Arbeitszeit", "Muggel" }));
            Assert.That(result.Entries[0].Aliases, Is.Null, "An empty cell gives no aliases at all.");
            Assert.That(result.Entries[1].Aliases, Is.EqualTo(new[] { "Muggels", "Muggeln" }));
            Assert.That(result.Entries[1].Note, Is.EqualTo("Harry Potter"));
            Assert.That(result.Added, Is.EqualTo(2));
            Assert.That(result.SkippedRows, Is.EqualTo(2), "A row without a term or without a value is left out.");
        });
    }

    [Test]
    public void MergingReplacesAnEntryWithTheSameTermAndKeepsTheOthers()
    {
        GlossaryEntry[] existing = [new("ARBEITSZEIT", "work time"), new("Gehalt", "salary")];

        var result = GlossaryImport.Apply(existing, TABLE, MAPPING, GlossaryImportMode.MERGE);

        Assert.Multiple(() =>
        {
            Assert.That(result.Entries.Select(entry => entry.Term), Is.EqualTo(new[] { "Arbeitszeit", "Gehalt", "Muggel" }), "A replaced entry keeps its place.");
            Assert.That(result.Entries[0].Value, Is.EqualTo("working time"));
            Assert.That(result.Replaced, Is.EqualTo(1));
            Assert.That(result.Added, Is.EqualTo(1));
            Assert.That(result.Removed, Is.Zero);
        });
    }

    [Test]
    public void ReplacingDropsAllEntriesTheGlossaryHeld()
    {
        GlossaryEntry[] existing = [new("Gehalt", "salary")];

        var result = GlossaryImport.Apply(existing, TABLE, MAPPING, GlossaryImportMode.REPLACE);

        Assert.Multiple(() =>
        {
            Assert.That(result.Entries.Select(entry => entry.Term), Is.EqualTo(new[] { "Arbeitszeit", "Muggel" }));
            Assert.That(result.Removed, Is.EqualTo(1));
        });
    }

    [Test]
    public void TheLastRowWinsWhenATableHoldsATermTwice()
    {
        var table = new GlossaryImportTable(["Deutsch", "English"], [["Urlaub", "holiday"], ["urlaub ", "vacation"]]);

        var result = GlossaryImport.Apply([], table, new GlossaryColumnMapping(0, 1), GlossaryImportMode.MERGE);

        Assert.Multiple(() =>
        {
            Assert.That(result.Entries, Has.Count.EqualTo(1));
            Assert.That(result.Entries[0].Value, Is.EqualTo("vacation"));
        });
    }

    [Test]
    public void AColumnOutsideTheTableIsRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GlossaryImport.Apply([], TABLE, new GlossaryColumnMapping(0, 7), GlossaryImportMode.MERGE));
    }

    [TestCase("Deutsch", CommonLanguages.DE_DE)]
    [TestCase(" English ", CommonLanguages.EN_US)]
    [TestCase("en-GB", CommonLanguages.EN_GB)]
    [TestCase("DE_CH", CommonLanguages.DE_CH)]
    [TestCase("fr", CommonLanguages.FR_FR)]
    public void AColumnNameGivesAwayItsLanguage(string columnName, CommonLanguages expected)
    {
        Assert.That(GlossaryImport.SuggestLanguage(columnName), Is.EqualTo(expected));
    }

    [TestCase("Begriff")]
    [TestCase("")]
    public void AColumnNameWithoutALanguageSuggestsNone(string columnName)
    {
        Assert.That(GlossaryImport.SuggestLanguage(columnName), Is.Null);
    }
}