using AIStudio.Dialogs.Settings;
using AIStudio.Tools.PluginSystem;
using AIStudio.Settings;
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
    protected override bool ShowProfileSelection => this.showFooterProfileSelection;
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
    private bool showFooterProfileSelection = true;
    
    private Dictionary<string, string> inputFields = new();
    private Dictionary<string, string> dropdownFields = new();
    private Dictionary<string, bool> switchFields = new();
    private Dictionary<string, WebContentState> webContentFields = new();
    private Dictionary<string, FileContentState> fileContentFields = new();
    
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
            this.showFooterProfileSelection = !assistantPlugin.HasEmbeddedProfileSelection;
        }

        foreach (var component in this.RootComponent!.Children)
        {
            switch (component.Type)
            {
                case AssistantUiCompontentType.TEXT_AREA:
                    if (component is AssistantTextArea textArea)
                    {
                        this.inputFields.Add(textArea.Name, textArea.PrefillText);
                    }
                    break;
                case AssistantUiCompontentType.DROPDOWN:
                    if (component is AssistantDropdown dropdown)
                    {
                        this.dropdownFields.Add(dropdown.Name, dropdown.Default.Value);
                    }
                    break;
                case AssistantUiCompontentType.SWITCH:
                    if (component is AssistantSwitch switchComponent)
                    {
                        this.switchFields.Add(switchComponent.Name, switchComponent.Value);
                    }
                    break;
                case AssistantUiCompontentType.WEB_CONTENT_READER:
                    if (component is AssistantWebContentReader webContent)
                    {
                        this.webContentFields.Add(webContent.Name, new WebContentState
                        {
                            Preselect = webContent.Preselect,
                            PreselectContentCleanerAgent = webContent.PreselectContentCleanerAgent,
                        });
                    }
                    break;
                case AssistantUiCompontentType.FILE_CONTENT_READER:
                    if (component is AssistantFileContentReader fileContent)
                    {
                        this.fileContentFields.Add(fileContent.Name, new FileContentState());
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
        foreach (var entry in this.webContentFields)
        {
            entry.Value.Content = string.Empty;
            entry.Value.AgentIsRunning = false;
        }
        foreach (var entry in this.fileContentFields)
        {
            entry.Value.Content = string.Empty;
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

        foreach (var component in this.RootComponent!.Children)
        {
            var userInput = string.Empty;
            var userDecision = false;
            switch (component.Type)
            {
                case AssistantUiCompontentType.TEXT_AREA:
                    if (component is AssistantTextArea textArea)
                    {
                        prompt += $"context:{Environment.NewLine}{textArea.UserPrompt}{Environment.NewLine}---{Environment.NewLine}";
                        if (this.inputFields.TryGetValue(textArea.Name, out userInput))
                        {
                            prompt += $"user prompt:{Environment.NewLine}{userInput}";
                        }
                    }
                    break;
                case AssistantUiCompontentType.DROPDOWN:
                    if (component is AssistantDropdown dropdown)
                    {
                        prompt += $"{Environment.NewLine}context:{Environment.NewLine}{dropdown.UserPrompt}{Environment.NewLine}---{Environment.NewLine}";
                        if (this.dropdownFields.TryGetValue(dropdown.Name, out userInput))
                        {
                            prompt += $"user prompt:{Environment.NewLine}{userInput}";
                        }
                    }
                    break;
                case AssistantUiCompontentType.SWITCH:
                    if (component is AssistantSwitch switchComponent)
                    {
                        prompt += $"{Environment.NewLine}context:{Environment.NewLine}{switchComponent.UserPrompt}{Environment.NewLine}---{Environment.NewLine}";
                        if (this.switchFields.TryGetValue(switchComponent.Name, out userDecision))
                        {
                            prompt += $"user decision:{Environment.NewLine}{userDecision}";
                        }
                    }
                    break;
                case AssistantUiCompontentType.WEB_CONTENT_READER:
                    if (component is AssistantWebContentReader webContent &&
                        this.webContentFields.TryGetValue(webContent.Name, out var webState))
                    {
                        if (!string.IsNullOrWhiteSpace(webContent.UserPrompt))
                        {
                            prompt += $"{Environment.NewLine}context:{Environment.NewLine}{webContent.UserPrompt}{Environment.NewLine}---{Environment.NewLine}";
                        }

                        if (!string.IsNullOrWhiteSpace(webState.Content))
                        {
                            prompt += $"user prompt:{Environment.NewLine}{webState.Content}";
                        }
                    }
                    break;
                case AssistantUiCompontentType.FILE_CONTENT_READER:
                    if (component is AssistantFileContentReader fileContent &&
                        this.fileContentFields.TryGetValue(fileContent.Name, out var fileState))
                    {
                        if (!string.IsNullOrWhiteSpace(fileContent.UserPrompt))
                        {
                            prompt += $"{Environment.NewLine}context:{Environment.NewLine}{fileContent.UserPrompt}{Environment.NewLine}---{Environment.NewLine}";
                        }

                        if (!string.IsNullOrWhiteSpace(fileState.Content))
                        {
                            prompt += $"user prompt:{Environment.NewLine}{fileState.Content}";
                        }
                    }
                    break;
                default:
                    prompt += $"{userInput}{Environment.NewLine}";
                    break;
            }
        }

        return prompt;
    }
    
    private string? ValidateProfileSelection(AssistantProfileSelection profileSelection, Profile profile)
    {
        if (profile == default || profile == Profile.NO_PROFILE)
        {
            if (!string.IsNullOrWhiteSpace(profileSelection.ValidationMessage))
                return profileSelection.ValidationMessage;

            return this.T("Please select one of your profiles.");
        }

        return null;
    }
    
    private async Task Submit()
    {
        this.CreateChatThread();
        var time = this.AddUserRequest(this.CollectUserPrompt());
        await this.AddAIResponseAsync(time);
    }

}
