namespace AIStudio.Components;

public class MudJustifiedText : MudText
{
    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        this.Align = Align.Justify;
        this.Style = "hyphens: auto; word-break: auto-phrase;";
        
        await base.OnInitializedAsync();
    }

    #endregion
}