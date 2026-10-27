using AIStudio.Tools.Glossaries;

namespace AIStudio.Tests.Tools.Glossaries;

/// <summary>
/// Checks how the text of a spreadsheet or CSV file becomes tables to import into a glossary.
/// </summary>
/// <remarks>
/// The runtime hands a spreadsheet over as one fenced CSV block per sheet, always with commas. A CSV
/// file arrives as it is, and German exports of Excel separate their cells with semicolons. Cells may
/// hold the delimiter or line breaks when quoted, and empty rows and columns around the data must not
/// turn into columns the user has to tell apart from the real ones.
/// </remarks>
[TestFixture]
public sealed class GlossaryParserTests
{
    [Test]
    public void EverySheetOfASpreadsheetBecomesATable()
    {
        const string CONTENT = """
                               ```csv
                               Deutsch,English
                               Arbeitszeit,working time
                               ```
                               ```csv
                               Begriff,Erklärung
                               Muggel,"Ein Mensch, der nicht zaubern kann."
                               ```
                               """;

        var tables = GlossaryParser.Parse(CONTENT);

        Assert.Multiple(() =>
        {
            Assert.That(tables, Has.Count.EqualTo(2));
            Assert.That(tables[0].Columns, Is.EqualTo(new[] { "Deutsch", "English" }));
            Assert.That(tables[1].Rows[0], Is.EqualTo(new[] { "Muggel", "Ein Mensch, der nicht zaubern kann." }));
        });
    }

    [Test]
    public void ACsvFileWithSemicolonsIsSplitAtTheSemicolons()
    {
        var tables = GlossaryParser.Parse("Deutsch;English\r\nArbeitszeit;working time\r\nGehalt, brutto;gross salary\r\n");

        Assert.Multiple(() =>
        {
            Assert.That(tables, Has.Count.EqualTo(1));
            Assert.That(tables[0].Rows, Has.Count.EqualTo(2));
            Assert.That(tables[0].Rows[1], Is.EqualTo(new[] { "Gehalt, brutto", "gross salary" }));
        });
    }

    [Test]
    public void AFencedCsvFileKeepsItsOwnDelimiter()
    {
        const string CONTENT = """
                               ```csv
                               Deutsch;English
                               Gehalt, brutto;gross salary
                               ```
                               """;

        var tables = GlossaryParser.Parse(CONTENT, isDelimitedTextFile: true);

        Assert.Multiple(() =>
        {
            Assert.That(tables, Has.Count.EqualTo(1), "The runtime fences a CSV file as it is, semicolons included.");
            Assert.That(tables[0].Rows[0], Is.EqualTo(new[] { "Gehalt, brutto", "gross salary" }));
        });
    }

    [Test]
    public void ASpreadsheetSheetIsAlwaysSplitAtCommas()
    {
        const string CONTENT = """
                               ```csv
                               Begriff;Variante,Wert
                               Muggel;Muggels,muggle
                               ```
                               """;

        var tables = GlossaryParser.Parse(CONTENT, isDelimitedTextFile: false);

        Assert.That(tables[0].Columns, Is.EqualTo(new[] { "Begriff;Variante", "Wert" }), "The runtime writes every sheet with commas, so a semicolon inside a cell must not split it.");
    }

    [Test]
    public void AQuotedCellKeepsItsLineBreakAndQuotes()
    {
        var tables = GlossaryParser.Parse("Term,Explanation\n\"Muggel\",\"Line one\nsays \"\"hello\"\"\"\n");

        Assert.That(tables[0].Rows[0][1], Is.EqualTo("Line one\nsays \"hello\""));
    }

    [Test]
    public void EmptyRowsAndColumnsAroundTheDataAreDropped()
    {
        var tables = GlossaryParser.Parse(",,,\n,Deutsch,,English\n,,,\n,Arbeitszeit,,working time\n");

        Assert.Multiple(() =>
        {
            Assert.That(tables[0].Columns, Is.EqualTo(new[] { "Deutsch", "English" }));
            Assert.That(tables[0].Rows, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void AFileWithOneColumnOrNoRowsYieldsNoTable()
    {
        Assert.Multiple(() =>
        {
            Assert.That(GlossaryParser.Parse("Deutsch\nArbeitszeit\n"), Is.Empty);
            Assert.That(GlossaryParser.Parse("Deutsch,English\n"), Is.Empty);
        });
    }
}