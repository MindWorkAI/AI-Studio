using AIStudio.Assistants;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// Tells the user what became of a document an assistant worked on, and lets them save the new document.
/// </summary>
/// <remarks>
/// It counts the sections which stayed as they were and names what the format of the document leaves alone.
/// The texts which name what the assistant did come from the assistant.
/// </remarks>
public partial class EditedDocumentSummary : MSGComponentBase
{
    /// <summary>
    /// The document the assistant worked on.
    /// </summary>
    [Parameter, EditorRequired]
    public EditedDocument Document { get; set; } = null!;

    /// <summary>
    /// What the result is called, e.g., "Translated document".
    /// </summary>
    [Parameter]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// What became of the document, e.g., into which language it was translated.
    /// </summary>
    [Parameter]
    public string DoneText { get; set; } = string.Empty;

    /// <summary>
    /// How many sections of the document the assistant changed.
    /// </summary>
    [Parameter]
    public string SectionsText { get; set; } = string.Empty;

    /// <summary>
    /// The text of the button which saves the new document.
    /// </summary>
    [Parameter]
    public string SaveText { get; set; } = string.Empty;

    /// <summary>
    /// Whether the new document is being saved right now, which disables the button.
    /// </summary>
    [Parameter]
    public bool IsSaving { get; set; }

    /// <summary>
    /// Called when the user wants to save the new document.
    /// </summary>
    [Parameter]
    public EventCallback OnSave { get; set; }

    private async Task Save() => await this.OnSave.InvokeAsync();
}