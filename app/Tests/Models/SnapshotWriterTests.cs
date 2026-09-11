using AIStudio.Tests.Models.Corpus;

namespace AIStudio.Tests.Models;

/// <summary>
/// Writes the capability snapshot anew.
/// </summary>
/// <remarks>
/// Marked explicit, so it never runs as part of the suite: it would make the characterization test
/// pass by rewriting what that test compares against. Run it by hand, from the IDE or with
/// "dotnet test --filter TakeTheSnapshotAnew", once a diff has been read and accepted, and commit
/// the new file together with the change which caused it.
/// </remarks>
[TestFixture]
[Explicit("Rewrites the file the characterization test compares against. Run it only after reading the diff.")]
public sealed class SnapshotWriterTests
{
    [Test]
    public void TakeTheSnapshotAnew()
    {
        var knownWrong = ExpectedChanges.ENTRIES.Select(change => (change.Provider, change.ModelId)).ToHashSet();
        var entries = ModelCorpus.ENTRIES.Where(entry => !knownWrong.Contains((entry.Provider, entry.ModelId)));

        File.WriteAllText(CapabilitySnapshot.FILE_PATH, CapabilitySnapshot.Render(entries));
        File.Delete(CapabilitySnapshot.ACTUAL_FILE_PATH);

        TestContext.Out.WriteLine($"Wrote {CapabilitySnapshot.FILE_PATH}. Read the diff before committing it.");
    }
}