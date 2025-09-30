using AIStudio.Dialogs.Settings;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.PluginSystem.Assistants;
using AIStudio.Tools.PluginSystem.Assistants.DataModel;
using Microsoft.AspNetCore.Components;

namespace AIStudio.Assistants.Dynamic;

public partial class AssistantDynamic : AssistantBaseCore<SettingsDialogDynamic>
{
    [Parameter] 
    public AssistantForm? RootComponent { get; set; } = null!;

    private string? inputText;
    private string title = string.Empty;
    private string description = string.Empty;
    private string systemPrompt = string.Empty;
    private bool allowProfiles = true;

    protected override string Title => this.title;
    protected override string Description => this.description;
    protected override string SystemPrompt => this.systemPrompt;
    protected override bool AllowProfiles => this.allowProfiles;
    public override Tools.Components Component { get; }
    protected override void OnInitialized()
    {
        var guid = Guid.Parse("958312de-a9e7-4666-901f-4d5b61647efb");
        var plugin = PluginFactory.RunningPlugins.FirstOrDefault(e => e.Id == guid);
        if (plugin is PluginAssistants assistantPlugin)
        {
            this.RootComponent = assistantPlugin.RootComponent;
            this.title = assistantPlugin.AssistantTitle;
            this.description = assistantPlugin.AssistantDescription;
            this.systemPrompt = assistantPlugin.SystemPrompt;
            this.allowProfiles = assistantPlugin.AllowProfiles;
        }
        base.OnInitialized();
    }

    protected override void ResetForm()
    {
        throw new NotImplementedException();
    }

    protected override bool MightPreselectValues()
    {
        throw new NotImplementedException();
    }

    protected override string SubmitText { get; }
    protected override Func<Task> SubmitAction { get; }
}