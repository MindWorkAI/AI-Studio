using AIStudio.Settings;
using AIStudio.Tools.ToolCallingSystem;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// Picks the tools of a run as an ordinary form field, next to the settings they belong to.
/// </summary>
/// <remarks>
/// The counterpart to the tool selection in the footer, which floats above a whole chat or
/// assistant. Where the tools belong to one specific setting — the instructions of a batch job,
/// say — they are easier to grasp right there, and a read-only field is the honest way to show
/// tools somebody else decided on.
/// </remarks>
public partial class ToolSelectionField : MSGComponentBase
{
    [Parameter]
    public AIStudio.Tools.Components Component { get; set; } = AIStudio.Tools.Components.CHAT;

    [Parameter]
    public HashSet<string> SelectedToolIds { get; set; } = [];

    [Parameter]
    public EventCallback<HashSet<string>> SelectedToolIdsChanged { get; set; }

    /// <summary>
    /// Shows the tools without letting the user change them.
    /// </summary>
    /// <remarks>
    /// For tools that were decided elsewhere, such as by a document analysis policy. The user
    /// still gets to see what the run will do.
    /// </remarks>
    [Parameter]
    public bool ReadOnly { get; set; }

    [Parameter]
    public bool Disabled { get; set; }

    [Parameter]
    public string? Label { get; set; }

    [Parameter]
    public string? Help { get; set; }

    [Inject]
    private ToolRegistry ToolRegistry { get; init; } = null!;

    private List<ConfigurationSelectData<string>> availableTools = [];

    protected override async Task OnInitializedAsync()
    {
        this.availableTools = (await this.ToolRegistry.GetCatalogAsync(this.Component))
            .Select(x => new ConfigurationSelectData<string>(x.Implementation.GetDisplayName(), x.Definition.Id))
            .ToList();

        this.ApplyFilters([], [ Event.CONFIGURATION_CHANGED ]);
        await base.OnInitializedAsync();
    }

    private bool IsToolLocked(string toolId) => !this.SettingsManager.IsToolActive(toolId);

    private async Task OptionChangedAsync(HashSet<string> updatedToolIds)
    {
        this.SelectedToolIds = ToolSelectionRules.NormalizeSelection(updatedToolIds);
        await this.SelectedToolIdsChanged.InvokeAsync(this.SelectedToolIds);
    }

    protected override async Task ProcessIncomingMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data) where T : default
    {
        switch (triggeredEvent)
        {
            case Event.CONFIGURATION_CHANGED:
                this.availableTools = (await this.ToolRegistry.GetCatalogAsync(this.Component))
                    .Select(x => new ConfigurationSelectData<string>(x.Implementation.GetDisplayName(), x.Definition.Id))
                    .ToList();

                await this.InvokeAsync(this.StateHasChanged);
                break;
        }
    }
}