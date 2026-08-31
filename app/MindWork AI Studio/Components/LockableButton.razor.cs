using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public partial class LockableButton : ConfigurationBaseCore
{
    [Parameter]
    public string Icon { get; set; } = Icons.Material.Filled.Info;
    
    [Parameter]
    public Func<Task> OnClickAsync { get; set; } = () => Task.CompletedTask;
    
    [Parameter]
    public Action OnClick { get; set; } = () => { };
    
    [Parameter]
    public string Text { get; set; } = string.Empty;
    
    [Parameter]
    public string Class { get; set; } = string.Empty;

    /// <summary>
    /// An optional tooltip for the button. It is not shown while the button is locked,
    /// because the lock icon explains the situation in that case.
    /// </summary>
    [Parameter]
    public string Tooltip { get; set; } = string.Empty;

    /// <summary>
    /// The visual variant of the button.
    /// </summary>
    [Parameter]
    public Variant ButtonVariant { get; set; } = Variant.Filled;

    /// <summary>
    /// The color of the button.
    /// </summary>
    [Parameter]
    public Color ButtonColor { get; set; } = Color.Primary;

    /// <summary>
    /// Should the default bottom margin be removed? Useful when the button is placed in a
    /// toolbar instead of a settings panel.
    /// </summary>
    [Parameter]
    public bool NoMargin { get; set; }

    #region Overrides of ConfigurationBase

    /// <inheritdoc />
    protected override bool Stretch => false;

    protected override string GetClassForBase => this.Class;

    protected override string MarginClass => this.NoMargin ? string.Empty : base.MarginClass;

    #endregion
    
    private async Task ClickAsync()
    {
        if (this.IsLocked() || this.Disabled())
            return;
        
        await this.OnClickAsync();
        this.OnClick();
    }
}