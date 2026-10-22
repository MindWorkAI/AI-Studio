using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// Shows a piece of content for the user to check: text formatted and as plain text, and an image,
/// a recording, or a video as itself.
/// </summary>
/// <remarks>
/// Every place which lets the user see what AI Studio reads or hands to the AI shows it the same
/// way, which is why this lives here instead of in each of them. A slim bar above the content says
/// what it is, and switches between the ways to see it; the caller fills its start, or names the
/// content there.<br/><br/>
/// The content scrolls inside its frame, so a dialog around it does not have to scroll as well. In a
/// dialog which fits the window, the frame gives way down to the room left over.<br/><br/>
/// The text is shown in full unless the caller sets a limit. Whether the beginning of a text is
/// enough depends on what the user wants to check: how a whole document was read, or exactly what
/// the AI received. Only the caller knows which, and how to explain a cut.<br/><br/>
/// Media and text may come together, e.g., a recording and its transcript: the media then comes
/// first, followed by the text.
/// </remarks>
public partial class ContentViewer : MSGComponentBase
{
    /// <summary>
    /// The ways to see the content.
    /// </summary>
    /// <remarks>
    /// Internal rather than private: Razor infers the type of the toggle items in a class of its own.
    /// </remarks>
    internal enum ContentView
    {
        MEDIA,
        FORMATTED,
        PLAIN,
    }

    /// <summary>
    /// What kind of content this is.
    /// </summary>
    [Parameter]
    public ContentViewerKind Kind { get; set; } = ContentViewerKind.TEXT;

    /// <summary>
    /// The text to show. For media, the text which belongs to it, e.g., a transcript, or nothing.
    /// </summary>
    [Parameter]
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// The address an image, a recording, or a video is loaded from. Not used for text.
    /// </summary>
    [Parameter]
    public string MediaUrl { get; set; } = string.Empty;

    /// <summary>
    /// What the content is called, shown in the bar above it unless the caller fills the bar itself.
    /// </summary>
    [Parameter]
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// What the bar above the content shows at its start in place of the label, e.g., where the
    /// content comes from and a button to open it there.
    /// </summary>
    [Parameter]
    public RenderFragment? Toolbar { get; set; }

    /// <summary>
    /// The hint below the text, or nothing.
    /// </summary>
    [Parameter]
    public string HelperText { get; set; } = string.Empty;

    /// <summary>
    /// How many characters of the text we show at most, or null to show all of it.
    /// </summary>
    [Parameter]
    public int? CharacterLimit { get; set; }

    /// <summary>
    /// Explains that the text was cut, given the number of characters left out. Null shows no explanation.
    /// </summary>
    [Parameter]
    public Func<int, string>? CutOffNotice { get; set; }

    /// <summary>
    /// The height which does not limit the content: it may take all the room its place leaves over,
    /// e.g., in a dialog which fits the window, see dialog-content-fit in app.css.
    /// </summary>
    public const string NO_HEIGHT_LIMIT = "none";

    /// <summary>
    /// How high the content may grow before it scrolls, as a CSS length.
    /// </summary>
    [Parameter]
    public string MaxHeight { get; set; } = "40vh";

    /// <summary>
    /// The ways to see this content, in the order they are offered; the first one is shown first.
    /// </summary>
    private readonly List<ContentView> views = [];

    /// <summary>
    /// The way the content is shown right now.
    /// </summary>
    private ContentView view;

    /// <summary>
    /// The text and the limit the current cut was made of, so that we cut once per text instead of once per render.
    /// </summary>
    private string? cutText;

    private int? cutLimit;

    /// <summary>
    /// What we show of the text: all of it, or its beginning.
    /// </summary>
    private string shownText = string.Empty;

    /// <summary>
    /// How many characters we left out. Zero when we show the entire text.
    /// </summary>
    private int cutOffCharacters;

    #region Overrides of ComponentBase

    protected override void OnParametersSet()
    {
        if (!ReferenceEquals(this.cutText, this.Text) || this.cutLimit != this.CharacterLimit)
        {
            this.cutText = this.Text;
            this.cutLimit = this.CharacterLimit;
            this.shownText = CutAtLineBreak(this.Text, this.CharacterLimit ?? int.MaxValue, out this.cutOffCharacters);
        }

        //
        // Media is seen as itself first. Text is seen formatted and as plain text, for text and for
        // media which brings some along. The way the user chose stays as long as it is offered:
        //
        this.views.Clear();
        if (this.Kind is not ContentViewerKind.TEXT)
            this.views.Add(ContentView.MEDIA);

        if (this.Kind is ContentViewerKind.TEXT || !string.IsNullOrWhiteSpace(this.Text))
            this.views.AddRange([ContentView.FORMATTED, ContentView.PLAIN]);

        if (!this.views.Contains(this.view))
            this.view = this.views[0];

        base.OnParametersSet();
    }

    #endregion

    private string GetViewName(ContentView contentView) => contentView switch
    {
        ContentView.MEDIA when this.Kind is ContentViewerKind.IMAGE => T("Image"),
        ContentView.MEDIA when this.Kind is ContentViewerKind.AUDIO => T("Audio"),
        ContentView.MEDIA => T("Video"),
        ContentView.FORMATTED => T("Formatted"),
        _ => T("Plain text"),
    };

    /// <summary>
    /// Takes the beginning of a text which is too long to show.
    /// </summary>
    /// <remarks>
    /// We cut at the last line break before the limit. Otherwise, we might tear apart a Markdown
    /// construct like a table row or a code fence in the middle of a line. A text without any line
    /// break before the limit is cut at the limit itself.
    /// </remarks>
    /// <param name="text">The text.</param>
    /// <param name="characterLimit">How many characters to keep at most.</param>
    /// <param name="cutOffCharacters">How many characters were left out; zero when nothing was.</param>
    /// <returns>The text itself when it fits, otherwise its beginning.</returns>
    internal static string CutAtLineBreak(string text, int characterLimit, out int cutOffCharacters)
    {
        if (text.Length <= characterLimit)
        {
            cutOffCharacters = 0;
            return text;
        }

        var cutIndex = text.LastIndexOf('\n', characterLimit - 1) + 1;
        if (cutIndex < 1)
            cutIndex = characterLimit;

        cutOffCharacters = text.Length - cutIndex;
        return text[..cutIndex];
    }
}