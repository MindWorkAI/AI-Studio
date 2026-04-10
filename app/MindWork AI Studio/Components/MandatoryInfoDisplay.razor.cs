using AIStudio.Settings.DataModel;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public partial class MandatoryInfoDisplay
{
    [Parameter]
    public DataMandatoryInfo Info { get; set; } = new();

    [Parameter]
    public DataMandatoryInfoAcceptance? Acceptance { get; set; }

    [Parameter]
    public bool ShowAcceptanceMetadata { get; set; }
}