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
    
    protected override string Title => this.title;
    protected override string Description => this.description;
    protected override string SystemPrompt => this.systemPrompt;
    protected override bool AllowProfiles => this.allowProfiles;
    protected override string SubmitText => this.submitText;
    protected override Func<Task> SubmitAction => this.Submit;
    public override Tools.Components Component { get; }

    private string? inputText;
    private string title = string.Empty;
    private string description = string.Empty;
    private string systemPrompt = string.Empty;
    private bool allowProfiles = true;
    private string submitText = string.Empty;
    private string selectedTargetLanguage = string.Empty;
    private string customTargetLanguage = string.Empty;
    
    private Dictionary<string, string> inputFields = new();
    
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
            this.submitText = assistantPlugin.SubmitText;
            this.allowProfiles = assistantPlugin.AllowProfiles;
        }

        foreach (var component in this.RootComponent!.Children)
        {
            switch (component.Type)
            {
                case AssistantUiCompontentType.TEXT_AREA:
                    if (component is AssistantTextArea textArea)
                    {
                        this.inputFields.Add(textArea.Name, string.Empty);
                    }
                    break;
            }
        }
        base.OnInitialized();
    }

    protected override void ResetForm()
    {
        foreach (var entry in this.inputFields)
        {
            this.inputFields[entry.Key] = string.Empty;
        }
    }

    protected override bool MightPreselectValues()
    {
        Console.WriteLine("throw new NotImplementedException();");
        return false;
    }

    private string? ValidateCustomLanguage(string value) => string.Empty;

    private string CollectUserPrompt()
    {
        var prompt = string.Empty;
        foreach (var entry in this.inputFields)
        {
            prompt += $"{entry.Value}{Environment.NewLine}";
        }
        return prompt;
    }
    
    private async Task Submit()
    {
        this.CreateChatThread();
        var time = this.AddUserRequest(
            $"""
                
                The given text is:
                
                ---
                {this.CollectUserPrompt()}
             """);
        
        await this.AddAIResponseAsync(time);
    }
}