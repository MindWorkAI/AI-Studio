using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public partial class Issues : MSGComponentBase
{
    [Parameter]
    public IEnumerable<string> IssuesData { get; set; } = [];

    /// <summary>
    /// Puts the icon of an issue at the first line of its text, like MudTextList does.
    /// </summary>
    private const string LIST_ITEM_STYLE = "display: flex; align-items: flex-start;";
}