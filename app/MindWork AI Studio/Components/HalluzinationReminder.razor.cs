using AIStudio.Tools.PluginSystem;
using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public partial class HalluzinationReminder: ComponentBase
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(HalluzinationReminder).Namespace, nameof(HalluzinationReminder));
    
    [Parameter]
    public string Text { get; set; } = TB("LLMs can make mistakes. Check important information.");

    [Parameter]
    public string ContainerClass { get; set; } = "mt-2 mb-1";
}