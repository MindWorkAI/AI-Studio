using AIStudio.Tools.PluginSystem;
using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public partial class HalluzinationReminder: ComponentBase
{
    private static string TA(string fallbackEN) => I18N.I.TA(fallbackEN, typeof(HalluzinationReminder).Namespace, nameof(HalluzinationReminder));
    
    [Parameter]
    public string Text { get; set; } = TA("LLMs can make mistakes. Check important information.");

    [Parameter]
    public string ContainerClass { get; set; } = "mt-2 mb-1";
}