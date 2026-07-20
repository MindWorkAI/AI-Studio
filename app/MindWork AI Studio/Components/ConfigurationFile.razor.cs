using AIStudio.Tools.Rust;
using AIStudio.Tools.Services;

using Microsoft.AspNetCore.Components;

using Timer = System.Timers.Timer;

namespace AIStudio.Components;

public partial class ConfigurationFile : ConfigurationBaseCore
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
    public string Icon { get; set; } = Icons.Material.Filled.AttachFile;

    /// <summary>
    /// The color of the icon to use.
    /// </summary>
    [Parameter]
    public Color IconColor { get; set; } = Color.Default;

    /// <summary>
    /// The title of the file selection dialog.
    /// </summary>
    [Parameter]
    public string FileDialogTitle { get; set; } = "Select File";

    /// <summary>
    /// The optional file type filter for the file selection dialog.
    /// </summary>
    [Parameter]
    public FileTypeFilter[]? Filter { get; set; }

    [Inject]
    private RustService RustService { get; init; } = null!;

    private string internalText = string.Empty;
    private bool isFileDialogOpen;
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

    private void InternalUpdate(string text)
    {
        this.timer.Stop();
        this.internalText = text;
        this.timer.Start();
    }

    private async Task OpenFileDialog()
    {
        if (this.isFileDialogOpen)
            return;

        this.isFileDialogOpen = true;
        try
        {
            var response = await this.RustService.SelectFile(this.FileDialogTitle, this.Filter, string.IsNullOrWhiteSpace(this.internalText) ? null : this.internalText);
            if (response.UserCancelled)
                return;

            this.timer.Stop();
            this.internalText = response.SelectedFilePath;
            await this.OptionChanged(response.SelectedFilePath);
        }
        finally
        {
            this.isFileDialogOpen = false;
        }
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