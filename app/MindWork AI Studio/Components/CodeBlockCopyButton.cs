using AIStudio.Tools.PluginSystem;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;

namespace AIStudio.Components;

/// <summary>
/// The copy button of a code block which AI Studio highlights itself, made like the one of
/// MudBlazor.Markdown, which is internal: the same classes, so that it appears at the same place while
/// the pointer is on the block, and the same confirmation for a moment after a click.
/// </summary>
/// <remarks>
/// It copies through the clipboard service of the app, the native clipboard of the runtime, which the
/// button of the library falls back to as well.
/// </remarks>
public sealed class CodeBlockCopyButton : ComponentBase
{
    private const string CSS_CLASS = "ma-2 mud-markdown-code-highlight-copybtn";

    private static readonly TimeSpan CONFIRMATION_DURATION = TimeSpan.FromSeconds(1.25);

    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(CodeBlockCopyButton).Namespace, nameof(CodeBlockCopyButton));

    /// <summary>
    /// The code to copy.
    /// </summary>
    [Parameter]
    public string Text { get; set; } = string.Empty;

    [Inject]
    private IMudMarkdownClipboardService ClipboardService { get; init; } = null!;

    private bool isCopied;

    /// <summary>
    /// What a copy button of a code block confirms with, those of MudBlazor.Markdown included.
    /// </summary>
    internal static string ConfirmationText => TB("Copied!");

    #region Overrides of ComponentBase

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        if (!this.isCopied)
        {
            builder.OpenComponent<MudIconButton>(0);
            builder.AddComponentParameter(1, nameof(MudIconButton.OnClick), EventCallback.Factory.Create<MouseEventArgs>(this, this.CopyAsync));
            builder.AddComponentParameter(2, nameof(MudIconButton.Class), CSS_CLASS);
            builder.AddComponentParameter(3, nameof(MudIconButton.Icon), Icons.Material.Rounded.ContentCopy);
            builder.AddComponentParameter(4, nameof(MudIconButton.Color), Color.Primary);
            builder.AddComponentParameter(5, nameof(MudIconButton.Variant), Variant.Filled);
            builder.AddComponentParameter(6, nameof(MudIconButton.Size), Size.Medium);
            builder.CloseComponent();
            return;
        }

        builder.OpenComponent<MudTooltip>(10);
        builder.AddComponentParameter(11, nameof(MudTooltip.Text), ConfirmationText);
        builder.AddComponentParameter(12, nameof(MudTooltip.Arrow), true);
        builder.AddComponentParameter(13, nameof(MudTooltip.Placement), Placement.Left);
        builder.AddComponentParameter(14, nameof(MudTooltip.Visible), true);
        builder.AddComponentParameter(15, nameof(MudTooltip.ShowOnClick), false);
        builder.AddComponentParameter(16, nameof(MudTooltip.ShowOnFocus), false);
        builder.AddComponentParameter(17, nameof(MudTooltip.ShowOnHover), false);
        builder.AddComponentParameter(18, nameof(MudTooltip.RootClass), CSS_CLASS);
        builder.AddComponentParameter(19, nameof(MudTooltip.ChildContent), (RenderFragment)(content =>
        {
            content.OpenComponent<MudIconButton>(0);
            content.AddComponentParameter(1, nameof(MudIconButton.Icon), Icons.Material.Rounded.DoneAll);
            content.AddComponentParameter(2, nameof(MudIconButton.Color), Color.Success);
            content.AddComponentParameter(3, nameof(MudIconButton.Variant), Variant.Filled);
            content.AddComponentParameter(4, nameof(MudIconButton.Size), Size.Medium);
            content.CloseComponent();
        }));

        builder.CloseComponent();
    }

    #endregion

    private async Task CopyAsync()
    {
        if (string.IsNullOrEmpty(this.Text))
            return;

        await this.ClipboardService.CopyToClipboardAsync(this.Text);

        this.isCopied = true;
        this.StateHasChanged();
        await Task.Delay(CONFIRMATION_DURATION);

        this.isCopied = false;
        this.StateHasChanged();
    }
}