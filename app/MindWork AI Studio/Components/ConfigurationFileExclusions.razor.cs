using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// Reuses ConfigurationText's persistence, validation, reset, and organization locks with the
/// shared exclusion editor. The editor supplies its own outlined fields and heading.
/// </summary>
public partial class ConfigurationFileExclusions : ConfigurationText
{
    [Parameter]
    public bool ShowHelp { get; set; } = true;

    protected override Variant Variant => Variant.Text;

    protected override string Label => string.Empty;
}
