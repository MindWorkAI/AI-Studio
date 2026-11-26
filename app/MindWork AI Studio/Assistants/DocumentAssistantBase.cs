using AIStudio.Components;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.AssistantLaunch;
using AIStudio.Tools.AssistantSessions;
using AIStudio.Tools.DocumentSegments;
using AIStudio.Tools.Rust;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace AIStudio.Assistants;

/// <summary>
/// An assistant which works on a whole document as well as on a text, e.g., translates either of them.
/// </summary>
/// <remarks>
/// <para>
/// It asks at the start what to work on, unless its settings decide, and a button next to its settings
/// switches between both. A document goes through the document segments, see the AGENTS.md in
/// Tools/DocumentSegments/, and becomes a new file of the same type, which the user saves. Everything this
/// takes lives here, so the assistant itself brings only its prompts, the texts which name what it does, and
/// its fields for a text.
/// </para>
/// <para>
/// An assistant which overrides ResetForm, CaptureCustomAssistantSessionState, or
/// RestoreCustomAssistantSessionState calls the base as well: it keeps the mode and the document.
/// </para>
/// <para>
/// The assistant renders its own form, see AssistantBaseCore, so the parts all of them share are fragments
/// here, which build their components in code: ModeChoice and DocumentSelection for the form, and the
/// switch next to the settings and the summary of the document for AssistantBase.
/// </para>
/// </remarks>
/// <typeparam name="TSettings">The dialog with the settings of the assistant.</typeparam>
public abstract partial class DocumentAssistantBase<TSettings> : AssistantBaseCore<TSettings> where TSettings : IComponent
{
    private static readonly AssistantSessionStateKey<AssistantContentMode> MODE_STATE_KEY = new(nameof(Mode));

    /// <summary>
    /// What the assistant works on right now; undecided until the user chose.
    /// </summary>
    protected AssistantContentMode Mode { get; private set; }

    private string TB(string fallbackEN) => this.T(fallbackEN, typeof(DocumentAssistantBase<>).Namespace, nameof(DocumentAssistantBase<>));

    private new string TA(string fallbackEN) => this.TA(fallbackEN, typeof(DocumentAssistantBase<>).Namespace, nameof(DocumentAssistantBase<>));

    #region What the assistant brings

    /// <summary>
    /// How the assistant starts, as its settings say.
    /// </summary>
    protected abstract AssistantStartMode StartMode { get; }

    /// <summary>
    /// The two options the user chooses from, one for the document and one for the text.
    /// </summary>
    protected abstract IReadOnlyList<AssistantModeOption<AssistantContentMode>> ModeOptions { get; }

    /// <summary>
    /// The tooltip of the button next to the settings, which switches to the given mode, e.g., "Switch to text translation".
    /// </summary>
    /// <param name="target">The mode the button switches to.</param>
    protected abstract string SwitchTooltip(AssistantContentMode target);

    /// <summary>
    /// What the model does with each text of a document, as the form stands when the run starts.
    /// </summary>
    protected abstract DocumentRequest CreateDocumentRequest();

    /// <summary>
    /// The title of the dialog in which the user selects a document.
    /// </summary>
    protected abstract string DocumentDialogTitle { get; }

    /// <summary>
    /// What the result of a document is called, e.g., "Translated document".
    /// </summary>
    protected abstract string DocumentResultTitle { get; }

    /// <summary>
    /// The text of the button which saves the new document, which titles the save dialog as well.
    /// </summary>
    protected abstract string DocumentSaveText { get; }

    /// <summary>
    /// Tells the user what became of the document, e.g., into which language it was translated.
    /// </summary>
    /// <param name="document">The document the assistant worked on.</param>
    protected abstract string DocumentDoneText(EditedDocument document);

    /// <summary>
    /// Tells the user how many sections of the document the assistant changed.
    /// </summary>
    /// <param name="results">What became of the segments of the document.</param>
    protected abstract string DocumentSectionsText(SegmentResults results);

    /// <summary>
    /// Puts a text which another assistant or a chat sent here into the field for the text.
    /// </summary>
    /// <remarks>
    /// The assistant works on the text then, so ApplyTextLaunchPayload switched to the text before.
    /// </remarks>
    /// <param name="payload">The text sent.</param>
    protected abstract void ApplyTextLaunchPayloadToText(TextLaunchPayload payload);

    /// <summary>
    /// The text of the last run and its result, which the assistant shows as a diff; empty for no diff.
    /// </summary>
    /// <remarks>
    /// The diff is no part of the form, so it leaves with the text and comes back with it.
    /// </remarks>
    protected virtual (string Original, string Changed) TextResultDiff => (string.Empty, string.Empty);

    #endregion

    #region Overrides of AssistantBase

    protected override bool ShowSubmit => this.Mode is not AssistantContentMode.UNDECIDED;

    protected override bool ShowReset => this.Mode is not AssistantContentMode.UNDECIDED;

    protected override bool ShowResult => this.Mode is AssistantContentMode.TEXT;

    protected override bool ShowCopyResult => this.Mode is AssistantContentMode.TEXT;

    protected override bool ShowSendTo => this.Mode is AssistantContentMode.TEXT;

    protected override bool ShowDedicatedProgress => this.Mode is AssistantContentMode.DOCUMENT;

    protected override string ProgressText => this.Mode is AssistantContentMode.DOCUMENT ? this.DocumentProgressText : string.Empty;

    // A document goes to the model in many small requests, which need no tools:
    protected override IReadOnlySet<string>? AssistantManagedToolIds => this.Mode is AssistantContentMode.DOCUMENT ? NO_TOOLS : null;

    protected override bool SubmitDisabled => this.isSavingDocument;

    protected sealed override void ApplyTextLaunchPayload(TextLaunchPayload payload)
    {
        this.Mode = AssistantContentMode.TEXT;
        this.ApplyTextLaunchPayloadToText(payload);
    }

    protected override async Task OnDefaultsAppliedAsync()
    {
        // A text sent here chose the text mode already, and a reset keeps the mode the user chose.
        // A session which is attached afterward brings its own mode:
        if (this.Mode is AssistantContentMode.UNDECIDED)
            this.Mode = this.StartMode.InitialContentMode;

        await base.OnDefaultsAppliedAsync();
    }

    protected override void ResetForm()
    {
        // The mode stays as it is: the button next to the settings switches it.
        this.documentPath = string.Empty;
        this.editedDocument = null;
        this.documentProgress = null;
    }

    /// <inheritdoc />
    protected override void CaptureCustomAssistantSessionState(AssistantSessionStateWriter state)
    {
        state.Set(MODE_STATE_KEY, this.Mode);
        state.Set(DOCUMENT_PATH_STATE_KEY, this.documentPath);
        state.Set(EDITED_DOCUMENT_STATE_KEY, this.editedDocument);
        state.Set(DOCUMENT_PROGRESS_STATE_KEY, this.documentProgress);
    }

    /// <inheritdoc />
    protected override void RestoreCustomAssistantSessionState(AssistantSessionStateReader state)
    {
        state.Restore(MODE_STATE_KEY, value => this.Mode = value);
        state.Restore(DOCUMENT_PATH_STATE_KEY, value => this.documentPath = value);
        state.Restore(EDITED_DOCUMENT_STATE_KEY, value => this.editedDocument = value);
        state.Restore(DOCUMENT_PROGRESS_STATE_KEY, value => this.documentProgress = value);
    }

    protected override async Task OnAssistantSessionRenderedAsync(AssistantSessionSnapshot snapshot)
    {
        if (!snapshot.IsActive)
            await this.ShowTextResultDiffAsync();

        await base.OnAssistantSessionRenderedAsync(snapshot);
    }

    private protected override RenderFragment? HeaderActions => this.Mode is not AssistantContentMode.UNDECIDED && this.SwitchOption is not null
        ? new RenderFragment(this.RenderModeSwitch)
        : null;

    private protected override RenderFragment? BelowSubmitContent => this.Mode is AssistantContentMode.DOCUMENT && !this.IsProcessing && this.editedDocument is not null
        ? new RenderFragment(this.RenderEditedDocumentSummary)
        : null;

    #endregion

    #region Parts of the form

    /// <summary>
    /// The cards with which the user chooses whether the assistant works on a document or on a text.
    /// </summary>
    private protected RenderFragment ModeChoice => this.RenderModeChoice;

    /// <summary>
    /// The field with which the user chooses the document to work on.
    /// </summary>
    private protected RenderFragment DocumentSelection => this.RenderDocumentSelection;

    private void RenderModeChoice(RenderTreeBuilder builder)
    {
        builder.OpenComponent<AssistantModeChoice<AssistantContentMode>>(0);
        builder.AddComponentParameter(1, nameof(AssistantModeChoice<>.Options), this.ModeOptions);
        builder.AddComponentParameter(2, nameof(AssistantModeChoice<>.ModeChosen), EventCallback.Factory.Create<AssistantContentMode>(this, this.ChooseMode));
        builder.CloseComponent();
    }

    private void RenderDocumentSelection(RenderTreeBuilder builder)
    {
        builder.OpenComponent<SelectFile>(0);
        builder.AddComponentParameter(1, nameof(SelectFile.File), this.documentPath);
        builder.AddComponentParameter(2, nameof(SelectFile.FileChanged), EventCallback.Factory.Create<string>(this, path => this.documentPath = path));
        builder.AddComponentParameter(3, nameof(SelectFile.Label), this.TB("Document"));
        builder.AddComponentParameter(4, nameof(SelectFile.FileDialogTitle), this.DocumentDialogTitle);
        builder.AddComponentParameter(5, nameof(SelectFile.Filter), new[] { FileTypes.EDITABLE_DOCUMENT });
        builder.AddComponentParameter(6, nameof(SelectFile.Validation), new Func<string, string?>(this.ValidateDocument));
        builder.AddComponentParameter(7, nameof(SelectFile.Disabled), this.IsProcessing);
        builder.AddComponentParameter(8, nameof(SelectFile.EnableDragDrop), true);
        builder.AddComponentParameter(9, nameof(SelectFile.CatchAllDocuments), true);
        builder.CloseComponent();
    }

    private void RenderModeSwitch(RenderTreeBuilder builder)
    {
        if (this.SwitchOption is not { } option)
            return;

        builder.OpenComponent<AssistantModeSwitch>(0);
        builder.AddComponentParameter(1, nameof(AssistantModeSwitch.Option), option);
        builder.AddComponentParameter(2, nameof(AssistantModeSwitch.Tooltip), this.SwitchTooltip(option.Mode));
        builder.AddComponentParameter(3, nameof(AssistantModeSwitch.Disabled), this.IsProcessing || this.SubmitDisabled);
        builder.AddComponentParameter(4, nameof(AssistantModeSwitch.ModeChosen), EventCallback.Factory.Create<AssistantContentMode>(this, this.ChooseMode));
        builder.CloseComponent();
    }

    private void RenderEditedDocumentSummary(RenderTreeBuilder builder)
    {
        if (this.editedDocument is not { } document)
            return;

        builder.OpenComponent<EditedDocumentSummary>(0);
        builder.AddComponentParameter(1, nameof(EditedDocumentSummary.Document), document);
        builder.AddComponentParameter(2, nameof(EditedDocumentSummary.Title), this.DocumentResultTitle);
        builder.AddComponentParameter(3, nameof(EditedDocumentSummary.DoneText), this.DocumentDoneText(document));
        builder.AddComponentParameter(4, nameof(EditedDocumentSummary.SectionsText), this.DocumentSectionsText(document.Results));
        builder.AddComponentParameter(5, nameof(EditedDocumentSummary.SaveText), this.DocumentSaveText);
        builder.AddComponentParameter(6, nameof(EditedDocumentSummary.IsSaving), this.isSavingDocument);
        builder.AddComponentParameter(7, nameof(EditedDocumentSummary.OnSave), EventCallback.Factory.Create(this, this.SaveEditedDocumentAsync));
        builder.CloseComponent();
    }

    #endregion

    /// <summary>
    /// The option of the mode the assistant does not work on right now, to which the button next to the settings switches.
    /// </summary>
    private AssistantModeOption<AssistantContentMode>? SwitchOption
    {
        get
        {
            foreach (var option in this.ModeOptions)
            {
                if (option.Mode != this.Mode)
                    return option;
            }

            return null;
        }
    }

    private async Task ChooseMode(AssistantContentMode chosenMode)
    {
        this.Mode = chosenMode;

        // The issues of the fields of the other mode would stay below the form otherwise:
        if (this.Form is not null)
            await this.Form.ResetValidationAsync();

        if (chosenMode is AssistantContentMode.TEXT)
        {
            await this.ShowTextResultDiffAsync();
            return;
        }

        var (original, changed) = this.TextResultDiff;
        if (!string.IsNullOrWhiteSpace(original) && !string.IsNullOrWhiteSpace(changed))
        {
            await this.JsRuntime.ClearDiv(BEFORE_RESULT_DIV_ID);
            await this.JsRuntime.ClearDiv(RESULT_DIV_ID);
        }
    }

    /// <summary>
    /// Shows the diff between the text of the last run and its result, while the assistant works on a text.
    /// </summary>
    protected async Task ShowTextResultDiffAsync()
    {
        var (original, changed) = this.TextResultDiff;
        if (this.IsAssistantComponentDisposed || this.Mode is not AssistantContentMode.TEXT || string.IsNullOrWhiteSpace(original) || string.IsNullOrWhiteSpace(changed))
            return;

        await this.JsRuntime.GenerateAndShowDiff(original, changed);
    }
}