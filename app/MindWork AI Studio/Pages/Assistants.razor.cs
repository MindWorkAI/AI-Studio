using AIStudio.Settings;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Pages;

public partial class Assistants : ComponentBase
{
    [Inject]
    public SettingsManager SettingsManager { get; set; } = null!;
}