using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public partial class EncryptionSecretInfo : ComponentBase
{
    [Parameter]
    public bool IsConfigured { get; set; }

    [Parameter]
    public string ConfiguredText { get; set; } = string.Empty;

    [Parameter]
    public string NotConfiguredText { get; set; } = string.Empty;

    [Parameter]
    public string Class { get; set; } = "mt-2 mb-2";
}