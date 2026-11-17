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