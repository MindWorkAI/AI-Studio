using AIStudio.Settings;
using AIStudio.Tools;
using AIStudio.Tools.ToolCallingSystem;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public partial class ToolDefaultsConfiguration : MSGComponentBase
{
    [Parameter]
    public AIStudio.Tools.Components Component { get; set; } = AIStudio.Tools.Components.CHAT;

    [Parameter]
    public bool IncludeVisibilityToggle { get; set; } = true;

    [Inject]
    private ToolRegistry ToolRegistry { get; init; } = null!;

    private List<ConfigurationSelectData<string>> availableTools = [];

    private string OptionTitle => this.Component is AIStudio.Tools.Components.CHAT ? this.T("Default tools for chat") : this.T("Default tools for this assistant");

    private string OptionHelp => this.Component is AIStudio.Tools.Components.CHAT
        ? this.T("Choose which tools should be preselected for new chats.")
        : this.T("Choose which tools should be preselected for new runs of this assistant.");

    protected override async Task OnInitializedAsync()
    {
        this.availableTools = (await this.ToolRegistry.GetCatalogAsync(this.Component))
            .Select(x => new ConfigurationSelectData<string>(x.Implementation.GetDisplayName(), x.Definition.Id))
            .ToList();
        await base.OnInitializedAsync();
    }

    private HashSet<string> GetSelectedValues() => this.SettingsManager.GetDefaultToolIds(this.Component);

    private void UpdateSelection(HashSet<string> values) => this.SettingsManager.ConfigurationData.Tools.DefaultToolIdsByComponent[this.Component.ToString()] = [..values];
}
