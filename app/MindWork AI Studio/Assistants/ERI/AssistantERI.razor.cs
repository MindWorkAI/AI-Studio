using System.Text;
using System.Text.RegularExpressions;

using AIStudio.Chat;
using AIStudio.Dialogs;
using AIStudio.Settings.DataModel;

using Microsoft.AspNetCore.Components;

using DialogOptions = AIStudio.Dialogs.DialogOptions;

namespace AIStudio.Assistants.ERI;

public partial class AssistantERI : AssistantBaseCore
{
    [Inject]
    private HttpClient HttpClient { get; set; } = null!;
    
    [Inject]
    private IDialogService DialogService { get; init; } = null!;
    
    public override Tools.Components Component => Tools.Components.ERI_ASSISTANT;
    
    protected override string Title => "ERI Server";
    
    protected override string Description =>
        """
        The ERI is the External Retrieval Interface for AI Studio and other tools. The ERI acts as a contract
        between decentralized data sources and, e.g., AI Studio. The ERI is implemented by the data sources,
        allowing them to be integrated into AI Studio later. This means that the data sources assume the server
        role and AI Studio (or any other LLM tool) assumes the client role of the API. This approach serves to
        realize a Retrieval-Augmented Generation (RAG) process with external data.
        """;
    
    protected override string SystemPrompt
    {
        get
        {
            var sb = new StringBuilder();
            
            //
            // ---------------------------------
            // Introduction
            // ---------------------------------
            //
            var programmingLanguage = this.selectedProgrammingLanguage is ProgrammingLanguages.OTHER ? this.otherProgrammingLanguage : this.selectedProgrammingLanguage.Name();
            sb.Append($"""
                       # Introduction
                       You are an experienced {programmingLanguage} developer. Your task is to implement an API server in
                       the {programmingLanguage} language according to the following OpenAPI Description (OAD):
                       
                       ```
                       {this.eriSpecification}
                       ```
                       
                       The server realizes the data retrieval component for a "Retrieval-Augmentation Generation" (RAG) process.
                       The server is called "{this.serverName}" and is described as follows:
                       
                       ```
                       {this.serverDescription}
                       ```
                       """);

            //
            // ---------------------------------
            // Data Source
            // ---------------------------------
            //
            
            sb.Append("""
                      
                      # Data Source
                      """);
            
            switch (this.selectedDataSource)
            {
                case DataSources.CUSTOM:
                    sb.Append($"""
                               The data source for the retrieval process is described as follows:
                               
                               ```
                               {this.otherDataSource}
                               ```
                               """);
                    
                    if(!string.IsNullOrWhiteSpace(this.dataSourceHostname))
                    {
                        sb.Append($"""
                                   
                                   The data source is accessible via the hostname `{this.dataSourceHostname}`.
                                   """);
                    }
                    
                    if(this.dataSourcePort is not null)
                    {
                        sb.Append($"""
                                   
                                   The data source is accessible via port `{this.dataSourcePort}`.
                                   """);
                    }
                    
                    break;
                
                case DataSources.FILE_SYSTEM:
                    sb.Append("""

                              The data source for the retrieval process is the local file system. Use a placeholder for the data source path.
                              """);
                    break;
                
                default:
                case DataSources.OBJECT_STORAGE:
                case DataSources.KEY_VALUE_STORE:
                case DataSources.DOCUMENT_STORE:
                case DataSources.RELATIONAL_DATABASE:
                case DataSources.GRAPH_DATABASE:
                    sb.Append($"""
                               The data source for the retrieval process is an "{this.dataSourceProductName}" database running on the
                               host `{this.dataSourceHostname}` and is accessible via port `{this.dataSourcePort}`.
                               """);
                    break;
            }

            //
            // ---------------------------------
            // Authentication and Authorization
            // ---------------------------------
            //
            
            sb.Append("""
                      
                      # Authentication and Authorization
                      The process for authentication and authorization is two-step. Step 1: Users must authenticate
                      with the API server using a `POST` call on `/auth` with the chosen method. If this step is
                      successful, the API server returns a token. Step 2: This token is then required for all
                      other API calls.
                      
                      Important notes:
                      - Calls to `/auth` and `/auth/methods` are accessible without authentication. All other API
                      endpoints require a valid step 2 token.
                      
                      - It is possible that a token (step 1 token) is desired as `authMethod`. These step 1 tokens
                      must never be accepted as valid tokens for step 2.
                      
                      - The OpenAPI Description (OAD) for `/auth` is not complete. This is because, at the time of
                      writing the OAD, it is not known what kind of authentication is desired. Therefore, you must
                      supplement the corresponding fields or data in the implementation. Example: If username/password
                      is desired, you must expect and read both. If both token and username/password are desired, you
                      must dynamically read the `authMethod` and expect and evaluate different fields accordingly.
                      
                      The following authentications and authorizations should be implemented for the API server:
                      """);
            
            foreach (var auth in this.selectedAuthenticationMethods)
                sb.Append($"- {auth.ToPrompt()}");
            
            if(this.IsUsingKerberos())
            {
                sb.Append($"""
                           
                           The server will run on {this.selectedOperatingSystem.Name()} operating systems. Keep
                           this in mind when implementing the SSO with Kerberos.
                           """);
            }

            //
            // ---------------------------------
            // Security
            // ---------------------------------
            //
            
            sb.Append($"""
                      
                      # Security
                      The following security requirement for `allowedProviderType` was chosen: `{this.allowedLLMProviders}`
                      """);
            
            //
            // ---------------------------------
            // Retrieval Processes
            // ---------------------------------
            //
            
            sb.Append($"""
                       
                       # Retrieval Processes
                       You are implementing the following data retrieval processes:
                       """);
            
            var retrievalProcessCounter = 1;
            foreach (var retrievalProcess in this.retrievalProcesses)
            {
                sb.Append($"""
                           
                           ## {retrievalProcessCounter++}. Retrieval Process
                           - Name: {retrievalProcess.Name}
                           - Description:
                           
                           ```
                           {retrievalProcess.Description}
                           ```
                           """);
                
                if(retrievalProcess.ParametersDescription?.Count > 0)
                {
                    sb.Append("""
                              
                              This retrieval process recognizes the following parameters:
                              """);
                    
                    var parameterCounter = 1;
                    foreach (var (parameter, description) in retrievalProcess.ParametersDescription)
                    {
                        sb.Append($"""
                                   
                                   - The {parameterCounter++} parameter is named "{parameter}":
                                   
                                   ```
                                   {description}
                                   ```
                                   """);
                    }

                    sb.Append("""

                              Please use sensible default values for the parameters. They are optional
                              for the user.
                              """);
                }
                
                if(retrievalProcess.Embeddings?.Count > 0)
                {
                    sb.Append("""
                               
                               The following embeddings are implemented for this retrieval process:
                               """);
                    
                    var embeddingCounter = 1;
                    foreach (var embedding in retrievalProcess.Embeddings)
                    {
                        sb.Append($"""
                                   
                                   - {embeddingCounter++}. Embedding
                                   - Name: {embedding.EmbeddingName}
                                   - Type: {embedding.EmbeddingType}
                                   
                                   - Description:
                                   
                                   ```
                                   {embedding.Description}
                                   ```
                                   
                                   - When used:
                                   
                                   ```
                                   {embedding.UsedWhen}
                                   ```
                                   """);
                    }
                }
            }

            //
            // ---------------------------------
            // Additional Libraries
            // ---------------------------------
            //
            
            if (!string.IsNullOrWhiteSpace(this.additionalLibraries))
            {
                sb.Append($"""
                           
                           # Additional Libraries
                           You use the following libraries for your implementation:
                           
                           {this.additionalLibraries}
                           """);
            }

            //
            // ---------------------------------
            // Remarks
            // ---------------------------------
            //
            
            sb.Append("""
                      
                      # Remarks
                      - You do not ask follow-up questions.
                      - You consider the security of the implementation by applying the Security by Design principle.
                      - Your output is formatted as Markdown. Code is formatted as code blocks. For every file, you
                        create a separate code block with its file path and name as chapter title.
                      """);
            
            return sb.ToString();
        }
    }

    protected override IReadOnlyList<IButtonData> FooterButtons => [];
    
    protected override bool ShowEntireChatThread => true;

    protected override string SubmitText => "Create the ERI server";

    protected override Func<Task> SubmitAction => this.GenerateServer;

    protected override bool SubmitDisabled => this.IsNoneERIServerSelected;

    protected override ChatThread ConvertToChatThread => (this.chatThread ?? new()) with
    {
        SystemPrompt = this.SystemPrompt,
    };
    
    protected override void ResetForm()
    {
        if (!this.MightPreselectValues())
        {
            this.serverName = string.Empty;
            this.serverDescription = string.Empty;
            this.selectedERIVersion = ERIVersion.V1;
            this.selectedProgrammingLanguage = ProgrammingLanguages.NONE;
            this.otherProgrammingLanguage = string.Empty;
            this.selectedDataSource = DataSources.NONE;
            this.dataSourceProductName = string.Empty;
            this.otherDataSource = string.Empty;
            this.dataSourceHostname = string.Empty;
            this.dataSourcePort = null;
            this.userTypedPort = false;
            this.selectedAuthenticationMethods = [];
            this.authDescription = string.Empty;
            this.selectedOperatingSystem = OperatingSystem.NONE;
            this.allowedLLMProviders = AllowedLLMProviders.NONE;
            this.embeddings = new();
            this.retrievalProcesses = new();
            this.additionalLibraries = string.Empty;
            this.writeToFilesystem = false;
            this.baseDirectory = string.Empty;
        }
    }
    
    protected override bool MightPreselectValues()
    {
        this.autoSave = this.SettingsManager.ConfigurationData.ERI.AutoSaveChanges;
        if (this.SettingsManager.ConfigurationData.ERI.PreselectOptions && this.selectedERIServer is not null)
        {
            this.serverName = this.selectedERIServer.ServerName;
            this.serverDescription = this.selectedERIServer.ServerDescription;
            this.selectedERIVersion = this.selectedERIServer.ERIVersion;
            this.selectedProgrammingLanguage = this.selectedERIServer.ProgrammingLanguage;
            this.otherProgrammingLanguage = this.selectedERIServer.OtherProgrammingLanguage;
            this.selectedDataSource = this.selectedERIServer.DataSource;
            this.dataSourceProductName = this.selectedERIServer.DataSourceProductName;
            this.otherDataSource = this.selectedERIServer.OtherDataSource;
            this.dataSourceHostname = this.selectedERIServer.DataSourceHostname;
            this.dataSourcePort = this.selectedERIServer.DataSourcePort;
            this.userTypedPort = this.selectedERIServer.UserTypedPort;

            var authMethods = new HashSet<Auth>(this.selectedERIServer.AuthMethods);
            this.selectedAuthenticationMethods = authMethods;
            
            this.authDescription = this.selectedERIServer.AuthDescription;
            this.selectedOperatingSystem = this.selectedERIServer.OperatingSystem;
            this.allowedLLMProviders = this.selectedERIServer.AllowedLLMProviders;
            this.embeddings = this.selectedERIServer.EmbeddingInfos;
            this.retrievalProcesses = this.selectedERIServer.RetrievalInfos;
            this.additionalLibraries = this.selectedERIServer.AdditionalLibraries;
            this.writeToFilesystem = this.selectedERIServer.WriteToFilesystem;
            this.baseDirectory = this.selectedERIServer.BaseDirectory;
            return true;
        }
        
        return false;
    }
    
    protected override async Task OnFormChange()
    {
        await this.AutoSave();
    }

    #region Overrides of AssistantBase

    protected override async Task OnInitializedAsync()
    {
        this.selectedERIServer = this.SettingsManager.ConfigurationData.ERI.ERIServers.FirstOrDefault();
        if(this.selectedERIServer is null)
        {
            await this.AddERIServer();
            this.selectedERIServer = this.SettingsManager.ConfigurationData.ERI.ERIServers.First();
        }

        await base.OnInitializedAsync();
    }

    #endregion

    private async Task AutoSave()
    {
        if(!this.autoSave || !this.SettingsManager.ConfigurationData.ERI.PreselectOptions)
            return;
        
        if(this.selectedERIServer is null)
            return;

        this.SettingsManager.ConfigurationData.ERI.PreselectedProvider = this.providerSettings.Id;
        this.selectedERIServer.ServerName = this.serverName;
        this.selectedERIServer.ServerDescription = this.serverDescription;
        this.selectedERIServer.ERIVersion = this.selectedERIVersion;
        this.selectedERIServer.ProgrammingLanguage = this.selectedProgrammingLanguage;
        this.selectedERIServer.OtherProgrammingLanguage = this.otherProgrammingLanguage;
        this.selectedERIServer.DataSource = this.selectedDataSource;
        this.selectedERIServer.DataSourceProductName = this.dataSourceProductName;
        this.selectedERIServer.OtherDataSource = this.otherDataSource;
        this.selectedERIServer.DataSourceHostname = this.dataSourceHostname;
        this.selectedERIServer.DataSourcePort = this.dataSourcePort;
        this.selectedERIServer.UserTypedPort = this.userTypedPort;
        this.selectedERIServer.AuthMethods = [..this.selectedAuthenticationMethods];
        this.selectedERIServer.AuthDescription = this.authDescription;
        this.selectedERIServer.OperatingSystem = this.selectedOperatingSystem;
        this.selectedERIServer.AllowedLLMProviders = this.allowedLLMProviders;
        this.selectedERIServer.EmbeddingInfos = this.embeddings;
        this.selectedERIServer.RetrievalInfos = this.retrievalProcesses;
        this.selectedERIServer.AdditionalLibraries = this.additionalLibraries;
        this.selectedERIServer.WriteToFilesystem = this.writeToFilesystem;
        this.selectedERIServer.BaseDirectory = this.baseDirectory;
        await this.SettingsManager.StoreSettings();
    }

    private DataERIServer? selectedERIServer;
    private bool autoSave;
    private string serverName = string.Empty;
    private string serverDescription = string.Empty;
    private ERIVersion selectedERIVersion = ERIVersion.V1;
    private string? eriSpecification;
    private ProgrammingLanguages selectedProgrammingLanguage = ProgrammingLanguages.NONE;
    private string otherProgrammingLanguage = string.Empty;
    private DataSources selectedDataSource = DataSources.NONE;
    private string otherDataSource = string.Empty;
    private string dataSourceProductName = string.Empty;
    private string dataSourceHostname = string.Empty;
    private int? dataSourcePort;
    private bool userTypedPort;
    private IEnumerable<Auth> selectedAuthenticationMethods = new HashSet<Auth>();
    private string authDescription = string.Empty;
    private OperatingSystem selectedOperatingSystem = OperatingSystem.NONE;
    private AllowedLLMProviders allowedLLMProviders = AllowedLLMProviders.NONE;
    private List<EmbeddingInfo> embeddings = new();
    private List<RetrievalInfo> retrievalProcesses = new();
    private string additionalLibraries = string.Empty;
    private bool writeToFilesystem;
    private string baseDirectory = string.Empty;

    private bool AreServerPresetsBlocked => !this.SettingsManager.ConfigurationData.ERI.PreselectOptions;
    
    private void SelectedERIServerChanged(DataERIServer? server)
    {
        this.selectedERIServer = server;
        this.ResetForm();
    }
    
    private async Task AddERIServer()
    {
        this.SettingsManager.ConfigurationData.ERI.ERIServers.Add(new ()
        {
            ServerName = $"ERI Server {DateTimeOffset.UtcNow}",
        });
        
        await this.SettingsManager.StoreSettings();
    }

    private async Task RemoveERIServer()
    {
        if(this.selectedERIServer is null)
            return;
        
        this.SettingsManager.ConfigurationData.ERI.ERIServers.Remove(this.selectedERIServer);
        this.selectedERIServer = null;
        this.ResetForm();
        
        await this.SettingsManager.StoreSettings();
        this.form?.ResetValidation();
    }
    
    private bool IsNoneERIServerSelected => this.selectedERIServer is null;

    /// <summary>
    /// Gets called when the server name was changed by typing.
    /// </summary>
    /// <remarks>
    /// This method is used to update the server name in the selected ERI server preset.
    /// Otherwise, the users would be confused when they change the server name and the changes are not reflected in the UI.
    /// </remarks>
    private void ServerNameWasChanged()
    {
        if(this.selectedERIServer is null)
            return;
        
        this.selectedERIServer.ServerName = this.serverName;
    }
    
    private string? ValidateServerName(string name)
    {
        if(string.IsNullOrWhiteSpace(name))
            return "Please provide a name for your ERI server. This name will be used to identify the server in AI Studio.";
        
        if(name.Length is > 60 or < 6)
            return "The name of your ERI server must be between 6 and 60 characters long.";
        
        if(this.SettingsManager.ConfigurationData.ERI.ERIServers.Where(n => n != this.selectedERIServer).Any(n => n.ServerName == name))
            return "An ERI server preset with this name already exists. Please choose a different name.";
        
        return null;
    }
    
    private string? ValidateServerDescription(string description)
    {
        if(string.IsNullOrWhiteSpace(description))
            return "Please provide a description for your ERI server. What data will the server retrieve? This description will be used to inform users about the purpose of your ERI server.";
        
        if(description.Length is < 32 or > 512)
            return "The description of your ERI server must be between 32 and 512 characters long.";
        
        return null;
    }
    
    private string? ValidateERIVersion(ERIVersion version)
    {
        if (version == ERIVersion.NONE)
            return "Please select an ERI specification version for the ERI server.";
        
        return null;
    }
    
    private string? ValidateProgrammingLanguage(ProgrammingLanguages language)
    {
        if (language == ProgrammingLanguages.OTHER)
            return null;
        
        if (language == ProgrammingLanguages.NONE)
            return "Please select a programming language for the ERI server.";
        
        return null;
    }
    
    private string? ValidateOtherLanguage(string language)
    {
        if(this.selectedProgrammingLanguage != ProgrammingLanguages.OTHER)
            return null;
        
        if(string.IsNullOrWhiteSpace(language))
            return "Please specify the custom programming language for the ERI server.";
        
        return null;
    }
    
    private string? ValidateDataSource(DataSources dataSource)
    {
        if (dataSource == DataSources.CUSTOM)
            return null;
        
        if (dataSource == DataSources.NONE)
            return "Please select a data source for the ERI server.";
        
        return null;
    }
    
    private string? ValidateDataSourceProductName(string productName)
    {
        if(this.selectedDataSource is DataSources.CUSTOM or DataSources.NONE or DataSources.FILE_SYSTEM)
            return null;
        
        if(string.IsNullOrWhiteSpace(productName))
            return "Please specify the product name of the data source, e.g., 'MongoDB', 'Redis', 'PostgreSQL', 'Neo4j', or 'MinIO', etc.";
        
        return null;
    }
    
    private string? ValidateOtherDataSource(string dataSource)
    {
        if(this.selectedDataSource != DataSources.CUSTOM)
            return null;
        
        if(string.IsNullOrWhiteSpace(dataSource))
            return "Please describe the data source of your ERI server.";
        
        return null;
    }
    
    private string? ValidateHostname(string hostname)
    {
        if(!this.NeedHostnamePort())
            return null;
        
        // When using a custom data source, the hostname is optional:
        if(this.selectedDataSource is DataSources.CUSTOM)
            return null;
        
        if(string.IsNullOrWhiteSpace(hostname))
            return "Please provide the hostname of the data source. Use 'localhost' if the data source is on the same machine as the ERI server.";
        
        if(hostname.Length > 255)
            return "The hostname of the data source must not exceed 255 characters.";
        
        return null;
    }
    
    private string? ValidatePort(int? port)
    {
        if(!this.NeedHostnamePort())
            return null;
        
        // When using a custom data source, the port is optional:
        if(this.selectedDataSource is DataSources.CUSTOM)
            return null;
        
        if(port is null)
            return "Please provide the port of the data source.";
        
        if(port is < 1 or > 65535)
            return "The port of the data source must be between 1 and 65535.";
        
        return null;
    }

    private void DataSourcePortWasTyped()
    {
        this.userTypedPort = true;
    }
    
    private void DataSourceWasChanged()
    {
        if(this.selectedERIServer is null)
            return;
        
        if (this.selectedDataSource is DataSources.NONE)
        {
            this.selectedERIServer.DataSourcePort = null;
            this.dataSourcePort = null;
            this.userTypedPort = false;
            return;
        }
        
        if(this.userTypedPort)
            return;
        
        //
        // Preselect the default port for the selected data source
        //
        this.dataSourcePort = this.selectedDataSource switch
        {
            DataSources.DOCUMENT_STORE => 27017,
            DataSources.KEY_VALUE_STORE => 6379,
            DataSources.OBJECT_STORAGE => 9000,
            DataSources.RELATIONAL_DATABASE => 5432,
            DataSources.GRAPH_DATABASE => 7687,
            
            _ => null
        };
    }
    
    private string? ValidateAuthenticationMethods(Auth _)
    {
        var authenticationMethods = (this.selectedAuthenticationMethods as HashSet<Auth>)!;
        if(authenticationMethods.Count == 0)
            return "Please select at least one authentication method for the ERI server.";
        
        return null;
    }
    
    private void AuthenticationMethodWasChanged(IEnumerable<Auth>? selectedValues)
    {
        if(selectedValues is null)
        {
            this.selectedAuthenticationMethods = [];
            this.selectedOperatingSystem = OperatingSystem.NONE;
            return;
        }

        this.selectedAuthenticationMethods = selectedValues;
        if(!this.IsUsingKerberos())
            this.selectedOperatingSystem = OperatingSystem.NONE;
    }
    
    private bool IsUsingKerberos()
    {
        return this.selectedAuthenticationMethods.Contains(Auth.KERBEROS);
    }
    
    private string? ValidateOperatingSystem(OperatingSystem os)
    {
        if(!this.IsUsingKerberos())
            return null;
        
        if(os is OperatingSystem.NONE)
            return "Please select the operating system on which the ERI server will run. This is necessary when using SSO with Kerberos.";
        
        return null;
    }
    
    private string? ValidateAllowedLLMProviders(AllowedLLMProviders provider)
    {
        if(provider == AllowedLLMProviders.NONE)
            return "Please select which types of LLMs users are allowed to use with the data from this ERI server.";
        
        return null;
    }
    
    private string AuthDescriptionTitle()
    {
        const string TITLE = "Describe how you planned the authentication process";
        return this.IsAuthDescriptionOptional() ? $"(Optional) {TITLE}" : TITLE;
    }

    private bool IsAuthDescriptionOptional()
    {
        if (this.selectedAuthenticationMethods is not HashSet<Auth> authenticationMethods)
            return true;
        
        if(authenticationMethods.Count > 1)
            return false;
        
        if (authenticationMethods.Any(n => n == Auth.NONE) && authenticationMethods.Count > 1)
            return false;
        
        return true;
    }

    private string? ValidateAuthDescription(string description)
    {
        var authenticationMethods = (this.selectedAuthenticationMethods as HashSet<Auth>)!;
        if(authenticationMethods.Any(n => n == Auth.NONE) && authenticationMethods.Count > 1 && string.IsNullOrWhiteSpace(this.authDescription))
            return "Please describe how the selected authentication methods should be used. Especially, explain for what data the NONE method (public access) is used.";
        
        if(authenticationMethods.Count > 1 && string.IsNullOrWhiteSpace(this.authDescription))
            return "Please describe how the selected authentication methods should be used.";
        
        return null;
    }
    
    private string GetMultiSelectionAuthText(List<Auth> selectedValues)
    {
        if(selectedValues.Count == 0)
            return "Please select at least one authentication method";
        
        if(selectedValues.Count == 1)
            return $"You have selected 1 authentication method";
        
        return $"You have selected {selectedValues.Count} authentication methods";
    }

    private bool NeedHostnamePort()
    {
        switch (this.selectedDataSource)
        {
            case DataSources.NONE:
            case DataSources.FILE_SYSTEM:
                return false;
            
            default:
                return true;
        }
    }
    
    private async Task AddEmbedding()
    {
        var dialogParameters = new DialogParameters<EmbeddingMethodDialog>
        {
            { x => x.IsEditing, false },
            { x => x.UsedEmbeddingMethodNames, this.embeddings.Select(n => n.EmbeddingName).ToList() },
        };
        
        var dialogReference = await this.DialogService.ShowAsync<EmbeddingMethodDialog>("Add Embedding Method", dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;

        var addedEmbedding = (EmbeddingInfo)dialogResult.Data!;
        this.embeddings.Add(addedEmbedding);
        await this.AutoSave();
    }
    
    private async Task EditEmbedding(EmbeddingInfo embeddingInfo)
    {
        var dialogParameters = new DialogParameters<EmbeddingMethodDialog>
        {
            { x => x.DataEmbeddingName, embeddingInfo.EmbeddingName },
            { x => x.DataEmbeddingType, embeddingInfo.EmbeddingType },
            { x => x.DataDescription, embeddingInfo.Description },
            { x => x.DataUsedWhen, embeddingInfo.UsedWhen },
            { x => x.DataLink, embeddingInfo.Link },
            
            { x => x.UsedEmbeddingMethodNames, this.embeddings.Where(n => n != embeddingInfo).Select(n => n.EmbeddingName).ToList() },
            { x => x.IsEditing, true },
        };

        var dialogReference = await this.DialogService.ShowAsync<EmbeddingMethodDialog>("Edit Embedding Method", dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;

        var editedEmbedding = (EmbeddingInfo)dialogResult.Data!;
        
        this.embeddings[this.embeddings.IndexOf(embeddingInfo)] = editedEmbedding;
        await this.AutoSave();
    }
    
    private async Task DeleteEmbedding(EmbeddingInfo embeddingInfo)
    {
        var message = this.retrievalProcesses.Any(n => n.Embeddings?.Contains(embeddingInfo) is true)
            ? $"The embedding '{embeddingInfo.EmbeddingName}' is used in one or more retrieval processes. Are you sure you want to delete it?"
            : $"Are you sure you want to delete the embedding '{embeddingInfo.EmbeddingName}'?";
        
        var dialogParameters = new DialogParameters
        {
            { "Message", message },
        };
        
        var dialogReference = await this.DialogService.ShowAsync<ConfirmDialog>("Delete Embedding", dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;
        
        this.retrievalProcesses.ForEach(n => n.Embeddings?.Remove(embeddingInfo));
        this.embeddings.Remove(embeddingInfo);
        
        await this.AutoSave();
    }
    
    private async Task AddRetrievalProcess()
    {
        var dialogParameters = new DialogParameters<RetrievalProcessDialog>
        {
            { x => x.IsEditing, false },
            { x => x.AvailableEmbeddings, this.embeddings },
            { x => x.UsedRetrievalProcessNames, this.retrievalProcesses.Select(n => n.Name).ToList() },
        };
        
        var dialogReference = await this.DialogService.ShowAsync<RetrievalProcessDialog>("Add Retrieval Process", dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;

        var addedRetrievalProcess = (RetrievalInfo)dialogResult.Data!;
        this.retrievalProcesses.Add(addedRetrievalProcess);
        await this.AutoSave();
    }
    
    private async Task EditRetrievalProcess(RetrievalInfo retrievalInfo)
    {
        var dialogParameters = new DialogParameters<RetrievalProcessDialog>
        {
            { x => x.DataName, retrievalInfo.Name },
            { x => x.DataDescription, retrievalInfo.Description },
            { x => x.DataLink, retrievalInfo.Link },
            { x => x.DataParametersDescription, retrievalInfo.ParametersDescription },
            { x => x.DataEmbeddings, retrievalInfo.Embeddings?.ToHashSet() },
            
            { x => x.IsEditing, true },
            { x => x.AvailableEmbeddings, this.embeddings },
            { x => x.UsedRetrievalProcessNames, this.retrievalProcesses.Where(n => n != retrievalInfo).Select(n => n.Name).ToList() },
        };

        var dialogReference = await this.DialogService.ShowAsync<RetrievalProcessDialog>("Edit Retrieval Process", dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;

        var editedRetrievalProcess = (RetrievalInfo)dialogResult.Data!;
        
        this.retrievalProcesses[this.retrievalProcesses.IndexOf(retrievalInfo)] = editedRetrievalProcess;
        await this.AutoSave();
    }
    
    private async Task DeleteRetrievalProcess(RetrievalInfo retrievalInfo)
    {
        var dialogParameters = new DialogParameters
        {
            { "Message", $"Are you sure you want to delete the retrieval process '{retrievalInfo.Name}'?" },
        };
        
        var dialogReference = await this.DialogService.ShowAsync<ConfirmDialog>("Delete Retrieval Process", dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;
        
        this.retrievalProcesses.Remove(retrievalInfo);
        await this.AutoSave();
    }

    [GeneratedRegex("""
                    "([\\/._\w-]+)"
                    """, RegexOptions.NonBacktracking)]
    private static partial Regex FileExtractRegex();

    private IEnumerable<string> ExtractFiles(string fileListAnswer)
    {
        //
        // We asked the LLM for answering using a specific JSON scheme.
        // However, the LLM might not follow this scheme. Therefore, we
        // need to parse the answer and extract the files.
        // The parsing strategy is to look for all strings.
        //
        var matches = FileExtractRegex().Matches(fileListAnswer);
        foreach (Match match in matches)
            if(match.Groups[1].Value is {} file && !string.IsNullOrWhiteSpace(file) && !file.Equals("files", StringComparison.OrdinalIgnoreCase))
                yield return file;
    }

    private async Task GenerateServer()
    {
        if(this.IsNoneERIServerSelected)
            return;
        
        await this.AutoSave();
        await this.form!.Validate();
        if (!this.inputIsValid)
            return;
        
        if(this.retrievalProcesses.Count == 0)
        {
            this.AddInputIssue("Please describe at least one retrieval process.");
            return;
        }
        
        this.eriSpecification = await this.selectedERIVersion.ReadSpecification(this.HttpClient);
        if (string.IsNullOrWhiteSpace(this.eriSpecification))
        {
            this.AddInputIssue("The ERI specification could not be loaded. Please try again later.");
            return;
        }
        
        var now = DateTimeOffset.UtcNow;
        this.CreateChatThread(KnownWorkspaces.ERI_SERVER_WORKSPACE_ID, $"{now:yyyy-MM-dd HH:mm} - {this.serverName}");
        
        //
        // ---------------------------------
        // Ask for files (viewable in the chat)
        // ---------------------------------
        //
        var time = this.AddUserRequest("""
                                       Please list all the files you want to create. Provide the result as a Markdown list.
                                       Start with a brief message that these are the files we are now creating.
                                       """, true);
        await this.AddAIResponseAsync(time);
        
        //
        // ---------------------------------
        // Ask for files, again (JSON output, invisible)
        // ---------------------------------
        //
        time = this.AddUserRequest("""
                                   Please format all the files you want to create as a JSON object, without Markdown.
                                   Use the following JSON schema:
                                   
                                   {
                                     [
                                         "path/to/file1",
                                         "path/to/file2"
                                     ]
                                   }
                                   """, true);
        var fileListAnswer = await this.AddAIResponseAsync(time, true);
        foreach (var file in this.ExtractFiles(fileListAnswer))
        {
            this.Logger.LogInformation($"The LLM want to create the file: '{file}'");
            
            //
            // ---------------------------------
            // Ask the AI to create another file
            // ---------------------------------
            //
            time = this.AddUserRequest($"""
                                        Please create the file `{file}`. Your output is formatted in Markdown
                                        using the following template:
                                        
                                        ## file/path
                                        
                                        ```language
                                        content of the file
                                        ```
                                        """, true);
            await this.AddAIResponseAsync(time);
        }
        
        await this.SendToAssistant(Tools.Components.CHAT, default);
    }
}