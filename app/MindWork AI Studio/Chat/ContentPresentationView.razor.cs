using AIStudio.Assistants.SWOTAnalysis;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Chat;

/// <summary>
/// Shows the structured presentation of a text, or the text itself when it has none.
/// </summary>
/// <remarks>
/// The one place which knows every kind of presentation and the view which shows it. Chats and
/// assistants render each text through this component, so a new kind needs a case here and in
/// Read, and nothing else in the chat.
/// </remarks>
public partial class ContentPresentationView
{
    /// <summary>
    /// The presentation of the text, if it has one.
    /// </summary>
    [Parameter]
    public ContentPresentation? Presentation { get; set; }

    /// <summary>
    /// How the text looks without a presentation, and when its data cannot be read.
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    private ContentPresentation? readPresentation;
    private int readDataHashCode;
    private object? data;

    #region Overrides of ComponentBase

    protected override void OnParametersSet()
    {
        //
        // Reading the data means deserializing it, so only a new presentation or changed data are
        // read again; a parent renders far more often than either happens:
        //
        var dataHashCode = this.Presentation?.GetDataHashCode() ?? 0;
        if (ReferenceEquals(this.Presentation, this.readPresentation) && dataHashCode == this.readDataHashCode)
            return;

        this.readPresentation = this.Presentation;
        this.readDataHashCode = dataHashCode;
        this.data = Read(this.Presentation);
    }

    #endregion

    /// <summary>
    /// Reads the data of a presentation into the type its view shows.
    /// </summary>
    /// <param name="presentation">The presentation to read.</param>
    /// <returns>The data, or null when there is no presentation or its data cannot be read.</returns>
    private static object? Read(ContentPresentation? presentation) => presentation?.Kind switch
    {
        ContentPresentationKind.SWOT_ANALYSIS => SwotAnalysisContentPresentation.TryRead(presentation, out var result) ? result : null,

        _ => null,
    };
}