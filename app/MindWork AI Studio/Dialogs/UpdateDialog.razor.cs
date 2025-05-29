using System.Reflection;

using AIStudio.Components;
using AIStudio.Tools.Metadata;
using AIStudio.Tools.Rust;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Dialogs;

/// <summary>
/// The update dialog that is used to inform the user about an available update.
/// </summary>
public partial class UpdateDialog : MSGComponentBase
{
    private static readonly Assembly ASSEMBLY = Assembly.GetExecutingAssembly();
    private static readonly MetaDataAttribute META_DATA = ASSEMBLY.GetCustomAttribute<MetaDataAttribute>()!;
    
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;
    
    [Parameter]
    public UpdateResponse UpdateResponse { get; set; }

    private string HeaderText => string.Format(T("Update from v{0} to v{1}"), META_DATA.Version, this.UpdateResponse.NewVersion);

    private void Cancel() => this.MudDialog.Cancel();
    
    private void Confirm() => this.MudDialog.Close(DialogResult.Ok(true));
}