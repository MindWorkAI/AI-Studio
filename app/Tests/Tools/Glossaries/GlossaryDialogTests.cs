using System.Reflection;

using AIStudio.Dialogs;

namespace AIStudio.Tests.Tools.Glossaries;

[TestFixture]
public sealed class GlossaryDialogTests
{
    [TestCase("Arbeitszeit", "")]
    [TestCase("Arbeitszeit", "  ")]
    [TestCase("", "working time")]
    [TestCase("  ", "working time")]
    public async Task ImportingWithAnUnfinishedEntryKeepsTheRowsAndReportsTheProblem(string term, string value)
    {
        var dialog = new GlossaryDialog();
        var unfinished = new GlossaryDialog.EntryRow { Term = term, Value = value, Note = "Keep this draft." };
        List<GlossaryDialog.EntryRow> rows =
        [
            new() { Term = "Urlaub", Value = "annual leave" },
            unfinished,
        ];
        typeof(GlossaryDialog).GetField("rows", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(dialog, rows);

        // No dialog service is installed: the import must stop before opening the file import dialog.
        var import = (Task)typeof(GlossaryDialog).GetMethod("ImportEntries", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(dialog, null)!;
        await import;

        var remainingRows = (List<GlossaryDialog.EntryRow>)typeof(GlossaryDialog).GetField("rows", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(dialog)!;
        var issues = (string[])typeof(GlossaryDialog).GetField("dataIssues", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(dialog)!;
        Assert.Multiple(() =>
        {
            Assert.That(remainingRows, Is.SameAs(rows));
            Assert.That(remainingRows, Has.Count.EqualTo(2));
            Assert.That(remainingRows[1].Term, Is.EqualTo(term));
            Assert.That(remainingRows[1].Value, Is.EqualTo(value));
            Assert.That(remainingRows[1].Note, Is.EqualTo("Keep this draft."));
            Assert.That(issues, Has.Length.EqualTo(1));
            Assert.That(issues[0], Does.Contain("1 entries lack a term or a value"));
        });
    }
}
