using Microsoft.AspNetCore.Components;

using Timer = System.Timers.Timer;

namespace AIStudio.Components;

public partial class ConfigurationText : ConfigurationBaseCore
{
    /// <summary>
    /// The text used for the textfield.
    /// </summary>
    [Parameter]
    public Func<string> Text { get; set; } = () => string.Empty;
    
    /// <summary>
    /// An action which is called when the text was changed.
    /// </summary>
    [Parameter]
    public Action<string> TextUpdate { get; set; } = _ => { };

    /// <summary>
    /// The icon to display next to the textfield.
    /// </summary>
    [Parameter]
    public string Icon { get; set; } = Icons.Material.Filled.Info;

    /// <summary>
    /// The color of the icon to use.
    /// </summary>
    [Parameter]
    public Color IconColor { get; set; } = Color.Default;

    /// <summary>
    /// How many lines should the textfield have?
    /// </summary>
    [Parameter]
    public int NumLines { get; set; } = 1;

    /// <summary>
    /// What is the maximum number of lines?
    /// </summary>
    [Parameter]
    public int MaxLines { get; set; } = 12;
    
    private string internalText = string.Empty;
    private readonly Timer timer = new(TimeSpan.FromMilliseconds(500))
    {
        AutoReset = false
    };
    
    #region Overrides of ConfigurationBase

    /// <inheritdoc />
    protected override bool Stretch => true;

    protected override Variant Variant => Variant.Outlined;
    
    protected override string Label => this.OptionDescription;

    #endregion

    #region Overrides of ConfigurationBase

    protected override async Task OnInitializedAsync()
    {
        this.timer.Elapsed += async (_, _) => await this.InvokeAsync(async () => await this.OptionChanged(this.internalText));
        await base.OnInitializedAsync();
    }

    protected override async Task OnParametersSetAsync()
    {
        this.internalText = this.Text();
        await base.OnParametersSetAsync();
    }

    #endregion

    private bool AutoGrow => this.NumLines > 1;
    
    private int GetMaxLines => this.AutoGrow ? this.MaxLines : 1;

    private void InternalUpdate(string text)
    {
        this.timer.Stop();
        this.internalText = text;
        this.timer.Start();
    }
    
    private async Task OptionChanged(string updatedText)
    {
        this.TextUpdate(updatedText);
        await this.SettingsManager.StoreSettings();
        await this.InformAboutChange();
    }

    #region Overrides of MSGComponentBase

    protected override void DisposeResources()
    {
        try
        {
            this.timer.Stop();
            this.timer.Dispose();
        }
        catch
        {
            // ignore
        }
        
        base.DisposeResources();
    }

    #endregion
}