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
    /// Should the dialog be in editing mode?
    /// </summary>
    [Parameter]
    public bool IsEditing { get; init; }
    
    [Parameter]
    public List<EntryItem> AdditionalMessages { get; set; } = [];
    
    [Inject]
    private ILogger<ProviderDialog> Logger { get; init; } = null!;
    
    private static readonly Dictionary<string, object?> SPELLCHECK_ATTRIBUTES = new();
    
    /// <summary>
    /// The list of used chat template names. We need this to check for uniqueness.
    /// </summary>
    private List<string> UsedNames { get; set; } = [];
    
    private bool dataIsValid;
    private string[] dataIssues = [];
    private string dataEditingPreviousName = string.Empty;
    
    private EntryItem messageEntryBeforeEdit;
    private readonly List<EntryItem> additionalMessagesEntries = [];
    private readonly List<string> availableRoles = ["User", "Assistant"];
    private bool initialAddButtonDisabled = false;
    
    // We get the form reference from Blazor code to validate it manually:
    private MudForm form = null!;

    private ChatTemplate CreateChatTemplateSettings() => new()
    {
        Num = this.DataNum,
        Id = this.DataId,
        
        Name = this.DataName,
        NeedToKnow = this.DataSystemPrompt,
        // AdditionalMessages = this.additionalMessagesEntries,
        Actions = string.Empty,
    };

    private void RemoveMessage(EntryItem item)
    {
        this.additionalMessagesEntries.Remove(item);
        this.Snackbar.Add("Entry removed", Severity.Info);
        this.initialAddButtonDisabled = this.additionalMessagesEntries.Count > 0;
        
        // ChatRoles.ChatTemplateRoles()  // TODO:  -> darauf foreach für alle Rollen in der Tabelle
    }

    private void AddInitialMessage()
    {
        var newEntry = new EntryItem
        {
            Role = availableRoles[0], // Default to first role ("User")
            Entry = "Your message"
        };

        this.additionalMessagesEntries.Add(newEntry);
        this.Snackbar.Add("Initial entry added", Severity.Success);
        this.initialAddButtonDisabled = this.additionalMessagesEntries.Count > 0;
    }

    private void AddNewMessageBelow(EntryItem currentItem)
    {
        
        // Create new entry with a valid role
        var newEntry = new EntryItem
        {
            Role = availableRoles.FirstOrDefault(role => role != currentItem.Role) ?? availableRoles[0], // Default to role not used in the previous entry
            Entry = "Your message"
        };

        // Rest of the method remains the same
        var index = this.additionalMessagesEntries.IndexOf(currentItem);

        if (index >= 0)
        {
            this.additionalMessagesEntries.Insert(index + 1, newEntry);
            this.Snackbar.Add("New entry added", Severity.Success);
        }
        else
        {
            this.additionalMessagesEntries.Add(newEntry);
            this.Snackbar.Add("New entry added", Severity.Success);
        }
        this.initialAddButtonDisabled = this.additionalMessagesEntries.Count > 0;
    }
    
    private void BackupItem(object element)
    {
        this.messageEntryBeforeEdit = new()
        {
            Role = ((EntryItem)element).Role,
            Entry = ((EntryItem)element).Entry
        };
    }

    private void ItemHasBeenCommitted(object element)
    {
        this.Snackbar.Add("Changes saved", Severity.Success);
    }

    private void ResetItemToOriginalValues(object element)
    {
        ((EntryItem)element).Role = this.messageEntryBeforeEdit.Role;
        ((EntryItem)element).Entry = this.messageEntryBeforeEdit.Entry;
    }

    public class EntryItem
    {
        public required string Role { get; set; }
        public required string Entry { get; set; }
    }

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
    
    private async Task Store()
    {
        await this.form.Validate();
        
        // When the data is not valid, we don't store it:
        if (!this.dataIsValid)
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
    
    private string? ValidateSystemPrompt(string text)
    {
        if (string.IsNullOrWhiteSpace(this.DataSystemPrompt))// && string.IsNullOrWhiteSpace(this.DataActions))
            return T("Please enter the system prompt.");
        
        if(text.Length > 444)
            return T("The text must not exceed 444 characters.");
        
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

    private void Cancel() => this.MudDialog.Cancel();
}