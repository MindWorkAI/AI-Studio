using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

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
    /// The icon to display next to the textfield, when it has a single line.
    /// </summary>
    [Parameter]
    public string Icon { get; set; } = Icons.Material.Filled.Info;

    /// <summary>
    /// Removes the default bottom margin when the field is part of a compact group.
    /// </summary>
    [Parameter]
    public bool Compact { get; set; }

    /// <summary>
    /// Wraps and grows with its text, including while the field is not focused, up to the lines of a
    /// field for file patterns.
    /// </summary>
    /// <remarks>
    /// Such a field shows the draft rather than the stored value. Otherwise, a reset which leaves the
    /// stored value as it was would not reach the field, e.g., restoring the default while the field
    /// shows a draft which the validation rejected.
    /// </remarks>
    [Parameter]
    public bool ExpandToContent { get; set; }

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

    /// <summary>
    /// When configured, displays a button which restores this value.
    /// </summary>
    [Parameter]
    public Func<string>? ResetValue { get; set; }

    /// <summary>
    /// The text displayed on the optional reset button.
    /// </summary>
    [Parameter]
    public string ResetButtonText { get; set; } = string.Empty;

    /// <summary>
    /// Uses an accessible reset icon inside the field.
    /// </summary>
    [Parameter]
    public bool ResetButtonIconOnly { get; set; }

    /// <summary>
    /// Validates the configured text before it is stored.
    /// </summary>
    [Parameter]
    public Func<string, string?>? Validation { get; set; }
    
    private string internalText = string.Empty;
    private string? lastSourceText;

    private readonly Timer timer = new(TimeSpan.FromMilliseconds(500))
    {
        AutoReset = false
    };

    protected string EditingText => this.internalText;
    
    #region Overrides of ConfigurationBase

    /// <inheritdoc />
    protected override bool Stretch => true;

    protected override Variant Variant => Variant.Outlined;

    protected override string MarginClass => this.Compact ? "mb-0" : base.MarginClass;
    
    protected override string Label => this.OptionDescription;

    protected override async Task OnInitializedAsync()
    {
        this.timer.Elapsed += (_, _) => this.InvokeAsync(async () => await this.OptionChanged(this.internalText)).Observe($"{nameof(ConfigurationText)}: applying the changed text");
        await base.OnInitializedAsync();
    }

    protected override async Task OnParametersSetAsync()
    {
        var sourceText = this.Text();
        // Re-rendering the parent must not replace a draft which is waiting for its debounce.
        // A genuinely changed setting, e.g. from a configuration plugin, still replaces it.
        if (this.IsDisabled || !string.Equals(sourceText, this.lastSourceText, StringComparison.Ordinal))
        {
            this.internalText = sourceText;
            this.lastSourceText = sourceText;
        }
        await base.OnParametersSetAsync();
    }

    #endregion

    private bool AutoGrow => this.ExpandToContent || this.NumLines > 1;

    private InputSizing Sizing => this.AutoGrow ? InputSizing.Auto : InputSizing.Fixed;

    private int GetMaxLines => this.ExpandToContent ? FilePatternSelection.MAX_FIELD_LINES : this.AutoGrow ? this.MaxLines : 1;

    private string InputClass => this.ExpandToContent ? "pattern-text-field" : string.Empty;

    //
    // MudBlazor centers the icon vertically, so in a field of several lines it floats in the middle
    // of the text. Such a field goes without:
    //
    private Adornment IconAdornment => this.NumLines == 1 ? Adornment.Start : Adornment.None;

    protected void InternalUpdate(string text)
    {
        if (this.IsDisabled)
            return;

        this.timer.Stop();
        this.internalText = text;
        this.timer.Start();
    }

    private EventCallback<MouseEventArgs> ResetAdornmentClick => this.ResetButtonIconOnly
        ? EventCallback.Factory.Create<MouseEventArgs>(this, this.ResetTextAsync)
        : default;

    protected async Task ResetTextAsync()
    {
        if (this.ResetValue is null || this.IsDisabled)
            return;

        await this.ApplyTextAsync(this.ResetValue());
    }

    /// <summary>
    /// Applies an explicit value immediately, including a reset of part of a compound setting.
    /// </summary>
    protected async Task ApplyTextAsync(string text)
    {
        if (this.IsDisabled)
            return;

        this.timer.Stop();
        this.internalText = text;
        await this.OptionChanged(this.internalText);
    }
    
    protected async Task OptionChanged(string updatedText)
    {
        if (this.IsDisabled || this.Validation?.Invoke(updatedText) is not null)
            return;

        this.TextUpdate(updatedText);
        // A save may finish after another keystroke. Remember the value it applied before
        // awaiting storage, so its configuration-change notification cannot erase that keystroke.
        this.lastSourceText = this.Text();
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