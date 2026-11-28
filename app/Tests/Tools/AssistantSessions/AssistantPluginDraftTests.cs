using AIStudio.Assistants;
using AIStudio.Assistants.Dynamic;
using AIStudio.Chat;
using AIStudio.Tools.AssistantSessions;
using AIStudio.Tools.PluginSystem.Assistants.DataModel;

namespace AIStudio.Tests.Tools.AssistantSessions;

/// <summary>
/// Checks the drafts of assistant plugins, whose whole form lives in one AssistantState.
/// </summary>
/// <remarks>
/// The plugin assistant captures a copy of its state with every snapshot. Compared by reference, such a
/// copy always looked changed, so that every plugin which the user merely opened kept a draft and brought
/// back old defaults. Shared with the form, it changed along with it.
/// </remarks>
[TestFixture]
public sealed class AssistantPluginDraftTests
{
    private static readonly AssistantSessionStateKey<AssistantState> ASSISTANT_STATE_KEY = new("AssistantState");

    [Test]
    public void APluginWithJustItsDefaultsKeepsNoDraft()
    {
        var form = CreateForm();

        Assert.That(AssistantLowerBase.IsWorthADraft(Capture(form), Capture(form), holdsSentText: false), Is.False, "Two copies of an unchanged form hold the same.");
    }

    [Test]
    public void AChangedTextOfAPluginIsWorthADraft()
    {
        var form = CreateForm();
        var stateWithDefaults = Capture(form);

        form.Text["topic"] = "Quarterly report";

        Assert.That(AssistantLowerBase.IsWorthADraft(Capture(form), stateWithDefaults, holdsSentText: false), Is.True);
    }

    [Test]
    public void AChangedAddressOfAWebPageIsWorthADraft()
    {
        var form = CreateForm();
        var stateWithDefaults = Capture(form);

        // The form changes the state of the web content in place:
        form.WebContent["page"].URL = "https://example.org";

        Assert.That(AssistantLowerBase.IsWorthADraft(Capture(form), stateWithDefaults, holdsSentText: false), Is.True, "A copy which shared the state of the web content would have changed along with the form and hidden the change.");
    }

    [Test]
    public void ARunningContentCleanerAloneIsNoChange()
    {
        var form = CreateForm();
        var stateWithDefaults = Capture(form);

        form.WebContent["page"].CleanerIsRunning = true;

        Assert.That(AssistantLowerBase.IsWorthADraft(Capture(form), stateWithDefaults, holdsSentText: false), Is.False, "Work in progress is not a change the user made.");
    }

    [Test]
    public void ACopySharesNothingWithTheForm()
    {
        var form = CreateForm();
        var copy = form.Clone();

        form.MultiSelect["formats"].Add("pdf");
        form.WebContent["page"].Content = "changed";
        form.FileContent["file"].Content = "changed";
        form.FileAttachments["files"].DocumentPaths.Add(new FileAttachment(FileAttachmentType.DOCUMENT, "report.pdf", "/tmp/report.pdf", 1));

        Assert.Multiple(() =>
        {
            Assert.That(copy.MultiSelect["formats"], Is.Empty);
            Assert.That(copy.WebContent["page"].Content, Is.Empty);
            Assert.That(copy.FileContent["file"].Content, Is.Empty);
            Assert.That(copy.FileAttachments["files"].DocumentPaths, Is.Empty);
        });
    }

    [Test]
    public void ClearingTheFlagsOfWorkInProgressKeepsTheInputs()
    {
        var form = CreateForm();
        form.WebContent["page"].URL = "https://example.org";
        form.WebContent["page"].Content = "The page";
        form.WebContent["page"].CleanerIsRunning = true;

        var draft = form.Clone();
        draft.ClearFlagsOfWorkInProgress();

        Assert.Multiple(() =>
        {
            Assert.That(draft.WebContent["page"].CleanerIsRunning, Is.False, "The cleaner ended with the component which started it, so its flag would keep the field locked for good.");
            Assert.That(draft.WebContent["page"].URL, Is.EqualTo("https://example.org"));
            Assert.That(draft.WebContent["page"].Content, Is.EqualTo("The page"));
            Assert.That(form.WebContent["page"].CleanerIsRunning, Is.True, "The draft is a copy; the form keeps its own flag.");
        });
    }

    private static AssistantState CreateForm()
    {
        var form = new AssistantState();
        form.Text["topic"] = string.Empty;
        form.MultiSelect["formats"] = [];
        form.Booleans["formal"] = false;
        form.WebContent["page"] = new WebContentState();
        form.FileContent["file"] = new FileContentState();
        form.FileAttachments["files"] = new FileAttachmentState();
        return form;
    }

    private static Dictionary<string, IAssistantSessionSnapshotField> Capture(AssistantState form)
    {
        var state = new AssistantSessionStateWriter();
        state.Set(ASSISTANT_STATE_KEY, form.Clone());
        return state.ToDictionary();
    }
}