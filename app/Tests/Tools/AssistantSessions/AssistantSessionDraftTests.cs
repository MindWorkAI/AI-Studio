using System.Reflection;

using AIStudio.Assistants;
using AIStudio.Tools;
using AIStudio.Tools.AssistantSessions;

namespace AIStudio.Tests.Tools.AssistantSessions;

/// <summary>
/// Checks the drafts which keep what an assistant holds while the user is elsewhere in AI Studio.
/// </summary>
/// <remarks>
/// A draft is no run: it must neither show up as one, e.g. as a status in the assistant overview, nor
/// outlive a reset of its assistant or a text sent to it, which would otherwise overwrite that text.
/// </remarks>
[TestFixture]
public sealed class AssistantSessionDraftTests
{
    private static readonly AssistantSessionStateKey<string> TEXT_KEY = new("Text");
    private static readonly AssistantSessionStateKey<bool> IS_READING_KEY = new("IsReading", isKeptOutOfDrafts: true);

    // The names under which AssistantBase captures whether its form is valid:
    private static readonly AssistantSessionStateKey<bool> INPUT_IS_VALID_KEY = new("InputIsValid");
    private static readonly AssistantSessionStateKey<string[]> INPUT_ISSUES_KEY = new("InputIssues");

    private static readonly AssistantSessionKey TRANSLATION_KEY = new(AIStudio.Tools.Components.TRANSLATION_ASSISTANT, "translation");
    private static readonly AssistantSessionKey SUMMARIZER_KEY = new(AIStudio.Tools.Components.TEXT_SUMMARIZER_ASSISTANT, "summarizer");

    private AssistantSessionService service = null!;

    [SetUp]
    public void SetUp() => this.service = new AssistantSessionService(MessageBus.INSTANCE);

    [Test]
    public void ADraftComesBackWithItsState()
    {
        this.service.SaveDraft(TRANSLATION_KEY, "Translation", null, StateWithText("hello"));

        var draft = this.service.TryTakeDraft(TRANSLATION_KEY);

        Assert.That(draft, Is.Not.Null);
        Assert.That(draft!.Key, Is.EqualTo(TRANSLATION_KEY));
        Assert.That(draft.IsActive, Is.False, "A draft is no run.");
        Assert.That(TextOf(draft), Is.EqualTo("hello"));
    }

    [Test]
    public void ADraftIsTakenOnlyOnce()
    {
        this.service.SaveDraft(TRANSLATION_KEY, "Translation", null, StateWithText("hello"));

        _ = this.service.TryTakeDraft(TRANSLATION_KEY);

        Assert.That(this.service.TryTakeDraft(TRANSLATION_KEY), Is.Null, "The assistant which took the draft holds it now, so it must not come back a second time.");
    }

    [Test]
    public void ANewerDraftReplacesTheOlderOne()
    {
        this.service.SaveDraft(TRANSLATION_KEY, "Translation", null, StateWithText("first"));
        this.service.SaveDraft(TRANSLATION_KEY, "Translation", null, StateWithText("second"));

        Assert.That(TextOf(this.service.TryTakeDraft(TRANSLATION_KEY)!), Is.EqualTo("second"));
    }

    [Test]
    public void ADraftKeepsItsStateWhenTheCapturedDictionaryChangesLater()
    {
        var state = StateWithText("hello");
        this.service.SaveDraft(TRANSLATION_KEY, "Translation", null, state);

        state.Clear();

        Assert.That(TextOf(this.service.TryTakeDraft(TRANSLATION_KEY)!), Is.EqualTo("hello"));
    }

    [Test]
    public void ADraftIsNoSession()
    {
        this.service.SaveDraft(TRANSLATION_KEY, "Translation", null, StateWithText("hello"));

        Assert.Multiple(() =>
        {
            Assert.That(this.service.GetSnapshots(), Is.Empty, "The assistant overview must show no status for a draft.");
            Assert.That(this.service.TryGetSnapshot(TRANSLATION_KEY), Is.Null);
            Assert.That(this.service.HasActiveSessions, Is.False, "A draft must not keep AI Studio from closing.");
        });
    }

    [Test]
    public async Task AResetRemovesTheDraft()
    {
        this.service.SaveDraft(TRANSLATION_KEY, "Translation", null, StateWithText("hello"));

        await this.service.ClearAsync(TRANSLATION_KEY);

        Assert.That(this.service.TryTakeDraft(TRANSLATION_KEY), Is.Null);
    }

    [Test]
    public async Task ATextSentToAnAssistantRemovesOnlyItsDraft()
    {
        this.service.SaveDraft(TRANSLATION_KEY, "Translation", null, StateWithText("hello"));
        this.service.SaveDraft(SUMMARIZER_KEY, "Summarizer", null, StateWithText("long text"));

        await this.service.ClearInactiveSessionsForComponentAsync(AIStudio.Tools.Components.TRANSLATION_ASSISTANT);

        Assert.Multiple(() =>
        {
            Assert.That(this.service.TryTakeDraft(TRANSLATION_KEY), Is.Null, "The text sent to the assistant replaces its draft.");
            Assert.That(this.service.TryTakeDraft(SUMMARIZER_KEY), Is.Not.Null, "The drafts of other assistants stay.");
        });
    }

    [Test]
    public void ADraftRestoresNoFlagOfWorkInProgress()
    {
        var state = StateWithText("hello");
        state[IS_READING_KEY.Name] = new AssistantSessionSnapshotField<bool>(true);

        var isReading = false;
        string? text = null;
        var reader = new AssistantSessionStateReader(state, "Translation", isDraft: true);
        reader.Restore(IS_READING_KEY, value => isReading = value);
        reader.Restore(TEXT_KEY, value => text = value);

        Assert.Multiple(() =>
        {
            Assert.That(isReading, Is.False, "The work ended with the component which did it, so its flag would keep the form locked for good.");
            Assert.That(text, Is.EqualTo("hello"), "Everything else comes back from a draft.");
        });
    }

    [Test]
    public void ARunRestoresFlagsOfWorkInProgress()
    {
        var state = StateWithText("hello");
        state[IS_READING_KEY.Name] = new AssistantSessionSnapshotField<bool>(true);

        var isReading = false;
        new AssistantSessionStateReader(state, "Translation").Restore(IS_READING_KEY, value => isReading = value);

        Assert.That(isReading, Is.True, "A run still goes on, so its flags hold.");
    }

    /// <summary>
    /// Keys whose name reads like a flag of work in progress but is none.
    /// </summary>
    private static readonly HashSet<string> NO_FLAGS_OF_WORK_IN_PROGRESS =
    [
        // Whether the meeting of the agenda assistant takes place online, which the user chooses:
        "IS_MEETING_VIRTUAL_STATE_KEY",
    ];

    [Test]
    public void EveryFlagOfWorkInProgressIsKeptOutOfDrafts()
    {
        var flags = typeof(AssistantLowerBase).Assembly.GetTypes()
            .Where(type => !type.ContainsGenericParameters)
            .SelectMany(type => type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            .Where(field => field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(AssistantSessionStateKey<>))
            .Where(field => ReadsLikeAFlagOfWorkInProgress(field.Name) && !NO_FLAGS_OF_WORK_IN_PROGRESS.Contains(field.Name))
            .ToList();

        Assert.That(flags.Select(field => field.Name), Does.Contain("IS_PROCESSING_STATE_KEY"), "The search must find the flags, or this test checks nothing.");

        var restoredFromDrafts = flags
            .Where(field => field.FieldType.GetProperty(nameof(AssistantSessionStateKey<>.IsKeptOutOfDrafts))!.GetValue(field.GetValue(null)) is not true)
            .Select(field => $"{field.DeclaringType!.Name}.{field.Name}")
            .ToList();

        Assert.That(restoredFromDrafts, Is.Empty, "Work outside of a run ends with the component which did it, so a flag restored from a draft stays set for good and may lock the form. Pass isKeptOutOfDrafts: true, or list a key which is no such flag in NO_FLAGS_OF_WORK_IN_PROGRESS.");
    }

    [Test]
    public void AnAssistantWithJustItsDefaultsKeepsNoDraft()
    {
        Assert.That(AssistantLowerBase.IsWorthADraft(StateWithText(string.Empty), StateWithText(string.Empty), holdsSentText: false), Is.False);
    }

    [Test]
    public void AChangedInputIsWorthADraft()
    {
        Assert.That(AssistantLowerBase.IsWorthADraft(StateWithText("hello"), StateWithText(string.Empty), holdsSentText: false), Is.True);
    }

    [Test]
    public void TheValidityOfTheFormAloneIsNoChange()
    {
        var stateWithDefaults = StateWithText(string.Empty);
        var state = StateWithText(string.Empty);
        state[INPUT_IS_VALID_KEY.Name] = new AssistantSessionSnapshotField<bool>(true);
        state[INPUT_ISSUES_KEY.Name] = new AssistantSessionSnapshotField<string[]>(["The text is missing."]);

        Assert.That(AssistantLowerBase.IsWorthADraft(state, stateWithDefaults, holdsSentText: false), Is.False, "Whether the form is valid follows from the inputs, which did not change.");
    }

    [Test]
    public void ATextSentToTheAssistantIsAlwaysWorthADraft()
    {
        Assert.That(AssistantLowerBase.IsWorthADraft(StateWithText(string.Empty), StateWithText(string.Empty), holdsSentText: true), Is.True, "The user chose to work on the text sent here, even before changing anything.");
    }

    private static bool ReadsLikeAFlagOfWorkInProgress(string name)
    {
        var words = name.Split('_');
        return words[0] switch
        {
            "IS" => words.Skip(1).Any(word => word.EndsWith("ING", StringComparison.Ordinal)),
            "EXECUTING" => true,
            _ => false,
        };
    }

    private static Dictionary<string, IAssistantSessionSnapshotField> StateWithText(string text)
    {
        var state = new AssistantSessionStateWriter();
        state.Set(TEXT_KEY, text);
        return state.ToDictionary();
    }

    private static string? TextOf(AssistantSessionSnapshot snapshot)
    {
        string? text = null;
        new AssistantSessionStateReader(snapshot.State, snapshot.Title).Restore(TEXT_KEY, value => text = value);
        return text;
    }
}