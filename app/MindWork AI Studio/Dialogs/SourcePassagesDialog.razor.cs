using AIStudio.Components;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.RAG;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Dialogs;

/// <summary>
/// Shows the passages the AI received from one file, so the user can check what an answer rests on.
/// </summary>
/// <remarks>
/// Opened from the list of sources below an answer, and meant for every other place which hands
/// passages of data sources to the AI and wants to show them, such as an assistant. The file is
/// named once, above everything else; its passages are numbered at the side when there are several,
/// in the order of the document, and the one picked is shown in full.<br/><br/>
/// Only the text of a passage scrolls: the dialog fits the window, see dialog-content-fit in app.css,
/// so that it never has to scroll as well.
/// </remarks>
public partial class SourcePassagesDialog : MSGComponentBase
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(SourcePassagesDialog).Namespace, nameof(SourcePassagesDialog));

    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    /// <summary>
    /// The passages to show, in the order the AI received them; they are shown in the order of the document.
    /// </summary>
    [Parameter]
    public IReadOnlyList<SourcePassage> Passages { get; set; } = [];

    /// <summary>
    /// The file the passages are from, or null when it is no file this computer can open.
    /// </summary>
    [Parameter]
    public SourceDocumentLocation? Document { get; set; }

    /// <summary>
    /// The passages in the order of the document. A passage which does not say where it is comes
    /// last, in the order the AI received it.
    /// </summary>
    /// <remarks>
    /// The order the AI received them in tells the user nothing: the AI gets no weight with them,
    /// and each one counts as much as every other. The order of the document is what lets the user
    /// read them alongside it.
    /// </remarks>
    private List<SourcePassage> passagesInDocumentOrder = [];

    private SourcePassage selectedPassage = new();

    private string fileName = string.Empty;

    private string path = string.Empty;

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        this.passagesInDocumentOrder = [..this.Passages.OrderBy(passage => passage.GetLocationRange()?.First ?? int.MaxValue)];
        this.selectedPassage = this.passagesInDocumentOrder.Count > 0 ? this.passagesInDocumentOrder[0] : new();
        this.fileName = GetFileName(this.Passages);
        this.path = this.Document?.Path ?? this.selectedPassage.Path;
        await base.OnInitializedAsync();
    }

    #endregion

    /// <summary>
    /// The name of the file the passages are from, without their pages: those are what the dialog lets the user pick.
    /// </summary>
    private static string GetFileName(IReadOnlyList<SourcePassage> passages) => passages is [{ DataSourceName.Length: > 0 } firstPassage, ..] ? firstPassage.DataSourceName : TB("Passages");

    private void Close() => this.MudDialog.Cancel();

    /// <summary>
    /// Shows the passages the AI received from a file.
    /// </summary>
    /// <param name="dialogService">The dialog service.</param>
    /// <param name="passages">The passages to show, in any order: they are shown in the order of the document.</param>
    /// <param name="document">The file the passages are from, or null when it is no file this computer can open.</param>
    public static async Task OpenDialogAsync(IDialogService dialogService, IReadOnlyList<SourcePassage> passages, SourceDocumentLocation? document = null)
    {
        var dialogParameters = new DialogParameters<SourcePassagesDialog>
        {
            { x => x.Passages, passages },
            { x => x.Document, document },
        };

        var dialogReference = await dialogService.ShowAsync<SourcePassagesDialog>(GetFileName(passages), dialogParameters, DialogOptions.FULLSCREEN);
        await dialogReference.Result;
    }
}