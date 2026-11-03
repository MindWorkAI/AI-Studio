using AIStudio.Tools.AssistantSessions;

namespace AIStudio.Tests.Tools.AssistantSessions;

/// <summary>
/// Checks which state fields a component takes over from a run it did not start.
/// </summary>
/// <remarks>
/// When the user leaves an assistant while it runs and comes back, the new component only shows the run,
/// while the old one keeps working out of sight. The form of the old one no longer changes, so the new
/// component must take over only what the run changed: otherwise, every report of the run undoes what
/// the user changed meanwhile. Wrongly taking a field for changed only restores it once more, while
/// wrongly taking it for the same loses an update, so whatever is not provably the same counts as changed.
/// </remarks>
[TestFixture]
public sealed class AssistantSessionSnapshotTests
{
    private static readonly AssistantSessionStateKey<string> TEXT_KEY = new("Text");
    private static readonly AssistantSessionStateKey<int> NUMBER_KEY = new("Number");
    private static readonly AssistantSessionStateKey<AssistantSessionStatus> STATUS_KEY = new("Status");
    private static readonly AssistantSessionStateKey<string?> OPTIONAL_TEXT_KEY = new("OptionalText");
    private static readonly AssistantSessionStateKey<List<string>> LINES_KEY = new("Lines");
    private static readonly AssistantSessionStateKey<HashSet<Note>> NOTES_KEY = new("Notes");
    private static readonly AssistantSessionStateKey<Dictionary<string, int>> COUNTS_KEY = new("Counts");
    private static readonly AssistantSessionStateKey<Note> NOTE_KEY = new("Note");
    private static readonly AssistantSessionStateKey<int> NUMBER_AS_TEXT_KEY = new("Text");

    private static readonly Guid SESSION_ID = Guid.NewGuid();

    private sealed record Note(string Text);

    [Test]
    public void WithoutAnEarlierSnapshotTheWholeStateIsTakenOver()
    {
        var snapshot = SnapshotOf(SESSION_ID, state =>
        {
            state.Set(TEXT_KEY, "title");
            state.Set(NUMBER_KEY, 3);
        });

        var changed = snapshot.StateChangedSince(null);

        Assert.That(changed.Keys, Is.EquivalentTo(new[] { TEXT_KEY.Name, NUMBER_KEY.Name }), "A component which attaches for the first time has nothing yet, so it takes over everything.");
    }

    [Test]
    public void AfterAnotherRunTheWholeStateIsTakenOver()
    {
        var previous = SnapshotOf(Guid.NewGuid(), state => state.Set(TEXT_KEY, "title"));
        var snapshot = SnapshotOf(SESSION_ID, state => state.Set(TEXT_KEY, "title"));

        var changed = snapshot.StateChangedSince(previous);

        Assert.That(changed.Keys, Is.EquivalentTo(new[] { TEXT_KEY.Name }), "What the component took over from another run says nothing about this one.");
    }

    [Test]
    public void UnchangedValuesAreLeftOut()
    {
        var previous = SnapshotOf(SESSION_ID, state =>
        {
            state.Set(TEXT_KEY, "title");
            state.Set(NUMBER_KEY, 3);
            state.Set(STATUS_KEY, AssistantSessionStatus.RUNNING);
        });

        var snapshot = SnapshotOf(SESSION_ID, state =>
        {
            state.Set(TEXT_KEY, new string("title".ToCharArray()));
            state.Set(NUMBER_KEY, 3);
            state.Set(STATUS_KEY, AssistantSessionStatus.RUNNING);
        });

        var changed = snapshot.StateChangedSince(previous);

        Assert.That(changed, Is.Empty, "The run left these fields alone, so the user may have changed them, and the component must keep that.");
    }

    [Test]
    public void ChangedAndNewValuesAreTakenOver()
    {
        var previous = SnapshotOf(SESSION_ID, state =>
        {
            state.Set(TEXT_KEY, "title");
            state.Set(NUMBER_KEY, 3);
            state.Set(STATUS_KEY, AssistantSessionStatus.RUNNING);
        });

        var snapshot = SnapshotOf(SESSION_ID, state =>
        {
            state.Set(TEXT_KEY, "title");
            state.Set(NUMBER_KEY, 4);
            state.Set(STATUS_KEY, AssistantSessionStatus.COMPLETED);
            state.Set(OPTIONAL_TEXT_KEY, "result");
        });

        var changed = snapshot.StateChangedSince(previous);

        Assert.That(changed.Keys, Is.EquivalentTo(new[] { NUMBER_KEY.Name, STATUS_KEY.Name, OPTIONAL_TEXT_KEY.Name }), "Progress and results come from the run, so they must arrive.");
    }

    [Test]
    public void CopiedCollectionsWithTheSameItemsAreLeftOut()
    {
        var note = new Note("first");
        List<string> lines = ["a", "b"];
        HashSet<Note> notes = [note];
        var counts = new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 };

        var previous = SnapshotOf(SESSION_ID, state =>
        {
            state.SetList(LINES_KEY, lines);
            state.SetHashSet(NOTES_KEY, notes);
            state.SetDictionary(COUNTS_KEY, counts);
        });

        var snapshot = SnapshotOf(SESSION_ID, state =>
        {
            state.SetList(LINES_KEY, lines);
            state.SetHashSet(NOTES_KEY, notes);
            state.SetDictionary(COUNTS_KEY, counts);
        });

        var changed = snapshot.StateChangedSince(previous);

        Assert.That(changed, Is.Empty, "The writer copies collections for every snapshot, so a new copy with the same items is no change.");
    }

    [Test]
    public void CollectionsWithOtherItemsAreTakenOver()
    {
        var previous = SnapshotOf(SESSION_ID, state =>
        {
            state.SetList(LINES_KEY, ["a", "b"]);
            state.SetHashSet(NOTES_KEY, [new Note("first")]);
            state.SetDictionary(COUNTS_KEY, new Dictionary<string, int> { ["a"] = 1 });
        });

        var snapshot = SnapshotOf(SESSION_ID, state =>
        {
            state.SetList(LINES_KEY, ["a", "c"]);
            state.SetHashSet(NOTES_KEY, [new Note("first"), new Note("second")]);
            state.SetDictionary(COUNTS_KEY, new Dictionary<string, int> { ["b"] = 1 });
        });

        var changed = snapshot.StateChangedSince(previous);

        Assert.That(changed.Keys, Is.EquivalentTo(new[] { LINES_KEY.Name, NOTES_KEY.Name, COUNTS_KEY.Name }));
    }

    [Test]
    public void NullIsTheSameOnlyAsNull()
    {
        var previous = SnapshotOf(SESSION_ID, state =>
        {
            state.Set(OPTIONAL_TEXT_KEY, null);
            state.Set(TEXT_KEY, "title");
        });

        var snapshot = SnapshotOf(SESSION_ID, state =>
        {
            state.Set(OPTIONAL_TEXT_KEY, null);
            state.Set(TEXT_KEY, null!);
        });

        var changed = snapshot.StateChangedSince(previous);

        Assert.That(changed.Keys, Is.EquivalentTo(new[] { TEXT_KEY.Name }));
    }

    [Test]
    public void AValueOfAnotherTypeIsTakenOver()
    {
        var previous = SnapshotOf(SESSION_ID, state => state.Set(TEXT_KEY, "3"));
        var snapshot = SnapshotOf(SESSION_ID, state => state.Set(NUMBER_AS_TEXT_KEY, 3));

        var changed = snapshot.StateChangedSince(previous);

        Assert.That(changed.Keys, Is.EquivalentTo(new[] { NUMBER_AS_TEXT_KEY.Name }), "A field captured with another type is another value, whatever it looks like.");
    }

    [Test]
    public void AnObjectIsTheSameOnlyAsItself()
    {
        var note = new Note("first");
        var previous = SnapshotOf(SESSION_ID, state => state.Set(NOTE_KEY, note));
        var sameNote = SnapshotOf(SESSION_ID, state => state.Set(NOTE_KEY, note));
        var equalNote = SnapshotOf(SESSION_ID, state => state.Set(NOTE_KEY, new Note(note.Text)));

        Assert.That(sameNote.StateChangedSince(previous), Is.Empty, "The run still holds the same object.");
        Assert.That(equalNote.StateChangedSince(previous).Keys, Is.EquivalentTo(new[] { NOTE_KEY.Name }), "An object may change without a new reference, so only its reference proves that it is the same.");
    }

    private static AssistantSessionSnapshot SnapshotOf(Guid sessionId, Action<AssistantSessionStateWriter> capture)
    {
        var state = new AssistantSessionStateWriter();
        capture(state);

        return new()
        {
            SessionId = sessionId,
            Key = new(AIStudio.Tools.Components.SLIDE_BUILDER_ASSISTANT, "test"),
            Title = "Test",
            Status = AssistantSessionStatus.RUNNING,
            StartedAt = DateTimeOffset.MinValue,
            UpdatedAt = DateTimeOffset.MinValue,
            State = state.ToDictionary(),
        };
    }
}