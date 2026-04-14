using System.Text;
using System.Text.Json;

using AIStudio.Components;
using AIStudio.Provider;
using AIStudio.Provider.HuggingFace;
using AIStudio.Tools.Services;
using AIStudio.Tools.Validation;

using Microsoft.AspNetCore.Components;

using Host = AIStudio.Provider.SelfHosted.Host;

namespace AIStudio.Dialogs;

/// <summary>
/// The provider settings dialog.
/// </summary>
public partial class ProviderDialog : MSGComponentBase, ISecretId
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    /// <summary>
    /// The provider's number in the list.
    /// </summary>
    [Parameter]
    public uint DataNum { get; set; }
    
    /// <summary>
    /// The provider's ID.
    /// </summary>
    [Parameter]
    public string DataId { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// The user chosen instance name.
    /// </summary>
    [Parameter]
    public string DataInstanceName { get; set; } = string.Empty;
    
    /// <summary>
    /// The chosen hostname for self-hosted providers.
    /// </summary>
    [Parameter]
    public string DataHostname { get; set; } = string.Empty;
    
    /// <summary>
    /// The host to use, e.g., llama.cpp.
    /// </summary>
    [Parameter]
    public Host DataHost { get; set; } = Host.NONE;
    
    /// <summary>
    /// The HFInstanceProvider to use, e.g., CEREBRAS.
    /// </summary>
    [Parameter]
    public HFInferenceProvider HFInferenceProviderId { get; set; } = HFInferenceProvider.NONE;
    
    /// <summary>
    /// Is this provider self-hosted?
    /// </summary>
    [Parameter]
    public bool IsSelfHosted { get; set; }
    
    /// <summary>
    /// The provider to use.
    /// </summary>
    [Parameter]
    public LLMProviders DataLLMProvider { get; set; } = LLMProviders.NONE;
    
    /// <summary>
    /// The LLM model to use, e.g., GPT-4o.
    /// </summary>
    [Parameter]
    public Model DataModel { get; set; }
    
    /// <summary>
    /// Should the dialog be in editing mode?
    /// </summary>
    [Parameter]
    public bool IsEditing { get; init; }
    
    [Parameter]
    public string AdditionalJsonApiParameters { get; set; } = string.Empty;
    
    [Inject]
    private RustService RustService { get; init; } = null!;

    [Inject]
    private ILogger<ProviderDialog> Logger { get; init; } = null!;

    private static readonly Dictionary<string, object?> SPELLCHECK_ATTRIBUTES = new();
    
    /// <summary>
    /// The list of used instance names. We need this to check for uniqueness.
    /// </summary>
    private List<string> UsedInstanceNames { get; set; } = [];
    
    private bool dataIsValid;
    private string[] dataIssues = [];
    private string dataAPIKey = string.Empty;
    private string dataManuallyModel = string.Empty;
    private string dataAPIKeyStorageIssue = string.Empty;
    private string dataEditingPreviousInstanceName = string.Empty;
    private string dataLoadingModelsIssue = string.Empty;
    private bool showExpertSettings;
    
    // We get the form reference from Blazor code to validate it manually:
    private MudForm form = null!;
    
    private readonly List<Model> availableModels = new();
    private readonly Encryption encryption = Program.ENCRYPTION;
    private readonly ProviderValidation providerValidation;

    public ProviderDialog()
    {
        this.providerValidation = new()
        {
            GetProvider = () => this.DataLLMProvider,
            GetAPIKeyStorageIssue = () => this.dataAPIKeyStorageIssue,
            GetPreviousInstanceName = () => this.dataEditingPreviousInstanceName,
            GetUsedInstanceNames = () => this.UsedInstanceNames,
            GetHost = () => this.DataHost,
            IsModelProvidedManually = () => this.DataLLMProvider.IsLLMModelProvidedManually(),
        };
    }

    private AIStudio.Settings.Provider CreateProviderSettings()
    {
        var cleanedHostname = this.DataHostname.Trim();

        // Determine the model based on the provider and host configuration:
        Model model;
        if (this.DataLLMProvider.IsLLMModelSelectionHidden(this.DataHost))
        {
            // Use system model placeholder for hosts that don't support model selection (e.g., llama.cpp):
            model = Model.SYSTEM_MODEL;
        }
        else if (this.DataLLMProvider is LLMProviders.FIREWORKS or LLMProviders.HUGGINGFACE)
        {
            // These providers require manual model entry:
            model = new Model(this.dataManuallyModel, null);
        }
        else
            model = this.DataModel;

        return new()
        {
            Num = this.DataNum,
            Id = this.DataId,
            InstanceName = this.DataInstanceName,
            UsedLLMProvider = this.DataLLMProvider,
            Model = model,
            IsSelfHosted = this.DataLLMProvider is LLMProviders.SELF_HOSTED,
            IsEnterpriseConfiguration = false,
            Hostname = cleanedHostname.EndsWith('/') ? cleanedHostname[..^1] : cleanedHostname,
            Host = this.DataHost,
            HFInferenceProvider = this.HFInferenceProviderId,
            AdditionalJsonApiParameters = this.AdditionalJsonApiParameters,
        };
    }

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        // Call the base initialization first so that the I18N is ready:
        await base.OnInitializedAsync();
        
        // Configure the spellchecking for the instance name input:
        this.SettingsManager.InjectSpellchecking(SPELLCHECK_ATTRIBUTES);
        
        // Load the used instance names:
        #pragma warning disable MWAIS0001
        this.UsedInstanceNames = this.SettingsManager.ConfigurationData.Providers.Select(x => x.InstanceName.ToLowerInvariant()).ToList();
        #pragma warning restore MWAIS0001

        this.showExpertSettings = !string.IsNullOrWhiteSpace(this.AdditionalJsonApiParameters);
        
        // When editing, we need to load the data:
        if(this.IsEditing)
        {
            this.dataEditingPreviousInstanceName = this.DataInstanceName.ToLowerInvariant();
            
            // When using Fireworks or Hugging Face, we must copy the model name:
            if (this.DataLLMProvider.IsLLMModelProvidedManually())
                this.dataManuallyModel = this.DataModel.Id;
            
            //
            // We cannot load the API key for self-hosted providers:
            //
            if (this.DataLLMProvider is LLMProviders.SELF_HOSTED && this.DataHost is not Host.OLLAMA && this.DataHost is not Host.VLLM)
            {
                await this.ReloadModels();
                await base.OnInitializedAsync();
                return;
            }
            
            // Load the API key:
            var requestedSecret = await this.RustService.GetAPIKey(this, SecretStoreType.LLM_PROVIDER, isTrying: this.DataLLMProvider is LLMProviders.SELF_HOSTED);
            if (requestedSecret.Success)
                this.dataAPIKey = await requestedSecret.Secret.Decrypt(this.encryption);
            else
            {
                this.dataAPIKey = string.Empty;
                if (this.DataLLMProvider is not LLMProviders.SELF_HOSTED)
                {
                    this.dataAPIKeyStorageIssue = string.Format(T("Failed to load the API key from the operating system. The message was: {0}. You might ignore this message and provide the API key again."), requestedSecret.Issue);
                    await this.form.Validate();
                }
            }

            await this.ReloadModels();
        }
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

    #region Implementation of ISecretId

    public string SecretId => this.DataLLMProvider.ToName();
    
    public string SecretName => this.DataInstanceName;

    #endregion

    private async Task Store()
    {
        await this.form.Validate();
        if (!string.IsNullOrWhiteSpace(this.dataAPIKeyStorageIssue))
            this.dataAPIKeyStorageIssue = string.Empty;

        // Manually validate the model selection (needed when no models are loaded
        // and the MudSelect is not rendered):
        var modelValidationError = this.providerValidation.ValidatingModel(this.DataModel);
        if (!string.IsNullOrWhiteSpace(modelValidationError))
        {
            this.dataIssues = [..this.dataIssues, modelValidationError];
            this.dataIsValid = false;
        }

        // When the data is not valid, we don't store it:
        if (!this.dataIsValid)
            return;
        
        // Use the data model to store the provider.
        // We just return this data to the parent component:
        var addedProviderSettings = this.CreateProviderSettings();
        if (!string.IsNullOrWhiteSpace(this.dataAPIKey))
        {
            // Store the API key in the OS secure storage:
            var storeResponse = await this.RustService.SetAPIKey(this, this.dataAPIKey, SecretStoreType.LLM_PROVIDER);
            if (!storeResponse.Success)
            {
                this.dataAPIKeyStorageIssue = string.Format(T("Failed to store the API key in the operating system. The message was: {0}. Please try again."), storeResponse.Issue);
                await this.form.Validate();
                return;
            }
        }

        this.MudDialog.Close(DialogResult.Ok(addedProviderSettings));
    }
    
    private string? ValidateManuallyModel(string manuallyModel)
    {
        if (this.DataLLMProvider.IsLLMModelProvidedManually() && string.IsNullOrWhiteSpace(manuallyModel))
            return T("Please enter a model name.");
        
        return null;
    }

    private void Cancel() => this.MudDialog.Cancel();

    private async Task OnAPIKeyChanged(string apiKey)
    {
        this.dataAPIKey = apiKey;
        if (!string.IsNullOrWhiteSpace(this.dataAPIKeyStorageIssue))
        {
            this.dataAPIKeyStorageIssue = string.Empty;
            await this.form.Validate();
        }
    }

    private void OnHostChanged(Host selectedHost)
    {
        // When the host changes, reset the model selection state:
        this.DataHost = selectedHost;
        this.DataModel = default;
        this.dataManuallyModel = string.Empty;
        this.availableModels.Clear();
        this.dataLoadingModelsIssue = string.Empty;
    }
    
    private async Task ReloadModels()
    {
        this.dataLoadingModelsIssue = string.Empty;
        var currentProviderSettings = this.CreateProviderSettings();
        var provider = currentProviderSettings.CreateProvider();
        if (provider is NoProvider)
            return;

        try
        {
            var result = await provider.GetTextModels(this.dataAPIKey);
            if (!result.Success)
                this.dataLoadingModelsIssue = result.FailureReason.ToUserMessage(T, provider.InstanceName);

            // Order descending by ID means that the newest models probably come first:
            var orderedModels = result.Models.OrderByDescending(n => n.Id);

            this.availableModels.Clear();
            this.availableModels.AddRange(orderedModels);
        }
        catch (Exception e)
        {
            this.Logger.LogError($"Failed to load models from provider '{this.DataLLMProvider}' (host={this.DataHost}, hostname='{this.DataHostname}'): {e.Message}");
            this.dataLoadingModelsIssue = T("We are currently unable to communicate with the provider to load models. Please try again later.");
        }
    }
    
    private string APIKeyText => this.DataLLMProvider switch
    {
        LLMProviders.SELF_HOSTED => T("(Optional) API Key"),
        _ => T("API Key"),
    };
    
    private void ToggleExpertSettings() => this.showExpertSettings = !this.showExpertSettings;

    private void OnInputChangeExpertSettings()
    {
        this.AdditionalJsonApiParameters = NormalizeAdditionalJsonApiParameters(this.AdditionalJsonApiParameters)
            .Trim()
            .TrimEnd(',', ' ');
    }

    private string? ValidateAdditionalJsonApiParameters(string additionalParams)
    {
        if (string.IsNullOrWhiteSpace(additionalParams))
            return null;

        var normalized = NormalizeAdditionalJsonApiParameters(additionalParams);
        if (!string.Equals(normalized, additionalParams, StringComparison.Ordinal))
            this.AdditionalJsonApiParameters = normalized;
        
        var json = $"{{{normalized}}}";
        try
        {
            if (!this.TryValidateJsonObjectWithDuplicateCheck(json, out var errorMessage))
                return errorMessage;

            return null;
        }
        catch (JsonException)
        {
            return T("Invalid JSON: Add the parameters in proper JSON formatting, e.g., \"temperature\": 0.5. Remove trailing commas. The usual surrounding curly brackets {} must not be used, though.");
        }
    }

    private static string NormalizeAdditionalJsonApiParameters(string input)
    {
        var sb = new StringBuilder(input.Length);
        var inString = false;
        var escape = false;
        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];
            if (inString)
            {
                sb.Append(c);
                if (escape)
                {
                    escape = false;
                    continue;
                }
                
                if (c == '\\')
                {
                    escape = true;
                    continue;
                }
                
                if (c == '"')
                    inString = false;
                
                continue;
            }
            
            if (c == '"')
            {
                inString = true;
                sb.Append(c);
                continue;
            }
            
            if (TryReadToken(input, i, "True", out var tokenLength))
            {
                sb.Append("true");
                i += tokenLength - 1;
                continue;
            }
            
            if (TryReadToken(input, i, "False", out tokenLength))
            {
                sb.Append("false");
                i += tokenLength - 1;
                continue;
            }
            
            if (TryReadToken(input, i, "Null", out tokenLength))
            {
                sb.Append("null");
                i += tokenLength - 1;
                continue;
            }
            
            sb.Append(c);
        }
        
        return sb.ToString();
    }

    private static bool TryReadToken(string input, int startIndex, string token, out int tokenLength)
    {
        tokenLength = 0;
        if (startIndex + token.Length > input.Length)
            return false;
        
        if (!input.AsSpan(startIndex, token.Length).SequenceEqual(token))
            return false;
        
        var beforeIndex = startIndex - 1;
        if (beforeIndex >= 0 && IsIdentifierChar(input[beforeIndex]))
            return false;
        
        var afterIndex = startIndex + token.Length;
        if (afterIndex < input.Length && IsIdentifierChar(input[afterIndex]))
            return false;
        
        tokenLength = token.Length;
        return true;
    }

    private static bool IsIdentifierChar(char c) => char.IsLetterOrDigit(c) || c == '_';
    
    private bool TryValidateJsonObjectWithDuplicateCheck(string json, out string? errorMessage)
    {
        errorMessage = null;
        var bytes = Encoding.UTF8.GetBytes(json);
        var reader = new Utf8JsonReader(bytes, new JsonReaderOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow
        });
        
        var objectStack = new Stack<HashSet<string>>();
        while (reader.Read())
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.StartObject:
                    objectStack.Push(new HashSet<string>(StringComparer.Ordinal));
                    break;
                
                case JsonTokenType.EndObject:
                    if (objectStack.Count > 0)
                        objectStack.Pop();
                    break;
                
                case JsonTokenType.PropertyName:
                    if (objectStack.Count == 0)
                    {
                        errorMessage = T("Additional API parameters must form a JSON object.");
                        return false;
                    }

                    var name = reader.GetString() ?? string.Empty;
                    if (!objectStack.Peek().Add(name))
                    {
                        errorMessage = string.Format(T("Duplicate key '{0}' found."), name);
                        return false;
                    }
                    break;
            }
        }

        if (objectStack.Count != 0)
        {
            errorMessage = T("Invalid JSON: Add the parameters in proper JSON formatting, e.g., \"temperature\": 0.5. Remove trailing commas. The usual surrounding curly brackets {} must not be used, though.");
            return false;
        }

        return true;
    }
    
    private string GetExpertStyles => this.showExpertSettings ? "border-2 border-dashed rounded pa-2" : string.Empty;

    private static string GetPlaceholderExpertSettings => 
      """
      "temperature": 0.5,
      "top_p": 0.9,
      "frequency_penalty": 0.0
      """;
}
