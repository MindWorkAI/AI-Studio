using AIStudio.Chat;
using AIStudio.Components;
using AIStudio.Settings;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Dialogs;

public partial class ChatTemplateDialog : MSGComponentBase
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    /// <summary>
    /// The chat template's number in the list.
    /// </summary>
    [Parameter]
    public uint DataNum { get; set; }
    
    /// <summary>
    /// The chat template's ID.
    /// </summary>
    [Parameter]
    public string DataId { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// The chat template name chosen by the user.
    /// </summary>
    [Parameter]
    public string DataName { get; set; } = string.Empty;
    
    /// <summary>
    /// What is the system prompt?
    /// </summary>
    [Parameter]
    public string DataSystemPrompt { get; set; } = string.Empty;
    
    /// <summary>
    /// What is the predefined user prompt?
    /// </summary>
    [Parameter]
    public string PredefinedUserPrompt { get; set; } = string.Empty;
    
    /// <summary>
    /// Should the dialog be in editing mode?
    /// </summary>
    [Parameter]
    public bool IsEditing { get; init; }
    
    [Parameter]
    public IReadOnlyCollection<ContentBlock> ExampleConversation { get; init; } = [];

    [Parameter] 
    public bool AllowProfileUsage { get; set; } = true;
    
    [Parameter] 
    public bool CreateFromExistingChatThread { get; set; }
    
    [Parameter] 
    public ChatThread? ExistingChatThread { get; set; }
    
    [Inject]
    private ILogger<ChatTemplateDialog> Logger { get; init; } = null!;
    
    private static readonly Dictionary<string, object?> SPELLCHECK_ATTRIBUTES = new();
    
    /// <summary>
    /// The list of used chat template names. We need this to check for uniqueness.
    /// </summary>
    private List<string> UsedNames { get; set; } = [];
    
    private bool dataIsValid;
    private List<ContentBlock> dataExampleConversation = [];
    private string[] dataIssues = [];
    private string dataEditingPreviousName = string.Empty;
    private bool isInlineEditOnGoing;

    private ContentBlock? messageEntryBeforeEdit;
    
    // We get the form reference from Blazor code to validate it manually:
    private MudForm form = null!;
    
    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        // Configure the spellchecking for the instance name input:
        this.SettingsManager.InjectSpellchecking(SPELLCHECK_ATTRIBUTES);
        
        // Load the used instance names:
        this.UsedNames = this.SettingsManager.ConfigurationData.ChatTemplates.Select(x => x.Name.ToLowerInvariant()).ToList();
        
        // When editing, we need to load the data:
        if(this.IsEditing)
        {
            this.dataEditingPreviousName = this.DataName.ToLowerInvariant();
            this.dataExampleConversation = this.ExampleConversation.Select(n => n.DeepClone()).ToList();
        }

        if (this.CreateFromExistingChatThread && this.ExistingChatThread is not null)
        {
            this.DataSystemPrompt = this.ExistingChatThread.SystemPrompt;
            this.dataExampleConversation = this.ExistingChatThread.Blocks.Select(n => n.DeepClone(true)).ToList();
            this.DataName = this.ExistingChatThread.Name;
        }
        
        await base.OnInitializedAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // Reset the validation when not editing and on the first render.
        // We don't want to show validation errors when the user opens the dialog.
        if(!this.IsEditing && firstRender)
            this.form.ResetValidation();
        
        await base.OnAfterRenderAsync(firstRender);
    }

    #endregion

    private ChatTemplate CreateChatTemplateSettings() => new()
    {
        Num = this.DataNum,
        Id = this.DataId,
        
        Name = this.DataName,
        SystemPrompt = this.DataSystemPrompt,
        PredefinedUserPrompt = this.PredefinedUserPrompt,
        ExampleConversation = this.dataExampleConversation,
        AllowProfileUsage = this.AllowProfileUsage,
        
        EnterpriseConfigurationPluginId = Guid.Empty,
        IsEnterpriseConfiguration = false,
    };

    private void RemoveMessage(ContentBlock item)
    {
        this.dataExampleConversation.Remove(item);
    }

    private void AddMessageToEnd()
    {
        var newEntry = new ContentBlock
        {
            Role = this.dataExampleConversation.Count is 0 ? ChatRole.USER : this.dataExampleConversation.Last().Role.SelectNextRoleForTemplate(), 
            Content = new ContentText(),
            ContentType = ContentType.TEXT,
            HideFromUser = true,
            Time = DateTimeOffset.Now,
        };

        this.dataExampleConversation.Add(newEntry);
    }

    private void AddMessageBelow(ContentBlock currentItem)
    {
        var insertedEntry = new ContentBlock
        {
            Role = this.dataExampleConversation.Count is 0 ? ChatRole.USER : this.dataExampleConversation.Last().Role.SelectNextRoleForTemplate(),
            Content = new ContentText(),
            ContentType = ContentType.TEXT,
            HideFromUser = true,
            Time = DateTimeOffset.Now,
        };
        
        // The rest of the method remains the same:
        var index = this.dataExampleConversation.IndexOf(currentItem);
        if (index >= 0)
            this.dataExampleConversation.Insert(index + 1, insertedEntry);
        else
            this.dataExampleConversation.Add(insertedEntry);
    }
    
    private void BackupItem(object? element)
    {
        this.isInlineEditOnGoing = true;
        this.messageEntryBeforeEdit = element switch
        {
            ContentBlock block => block.DeepClone(),
            _ => null,
        };
        
        this.StateHasChanged();
    }

    private void ResetItem(object? element)
    {
        this.isInlineEditOnGoing = false;
        switch (element)
        {
            case ContentBlock block:
                if (this.messageEntryBeforeEdit is null)
                    return; // No backup to restore from
                
                block.Content = this.messageEntryBeforeEdit.Content?.DeepClone();
                block.Role = this.messageEntryBeforeEdit.Role;
                break;
        }
        
        this.StateHasChanged();
    }

    private void CommitInlineEdit(object? element)
    {
        this.isInlineEditOnGoing = false;
        this.StateHasChanged();
    }
    
    private async Task Store()
    {
        await this.form.Validate();
        
        // When the data is not valid, we don't store it:
        if (!this.dataIsValid)
            return;
        
        // When an inline edit is ongoing, we cannot store the data:
        if (this.isInlineEditOnGoing)
            return;
        
        // Use the data model to store the chat template.
        // We just return this data to the parent component:
        var addedChatTemplateSettings = this.CreateChatTemplateSettings();
        
        if(this.IsEditing)
            this.Logger.LogInformation($"Edited chat template '{addedChatTemplateSettings.Name}'.");
        else
            this.Logger.LogInformation($"Created chat template '{addedChatTemplateSettings.Name}'.");
        
        this.MudDialog.Close(DialogResult.Ok(addedChatTemplateSettings));
    }
    
    private string? ValidateExampleTextMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return T("Please enter a message for the example conversation.");
        
        return null;
    }

    private string? ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return T("Please enter a name for the chat template.");
        
        if (name.Length > 40)
            return T("The chat template name must not exceed 40 characters.");
        
        // The instance name must be unique:
        var lowerName = name.ToLowerInvariant();
        if (lowerName != this.dataEditingPreviousName && this.UsedNames.Contains(lowerName))
            return T("The chat template name must be unique; the chosen name is already in use.");
        
        return null;
    }

    private void UseDefaultSystemPrompt()
    {
        this.DataSystemPrompt = SystemPrompts.DEFAULT;
    }

    private void Cancel() => this.MudDialog.Cancel();
}