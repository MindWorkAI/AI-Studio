using AIStudio.Chat;

namespace AIStudio.Assistants.EDI;

public partial class AssistantEDI : AssistantBaseCore
{
    public override Tools.Components Component => Tools.Components.EDI_ASSISTANT;
    
    protected override string Title => "EDI Server";
    
    protected override string Description =>
        """
        The EDI is the (E)xternal (D)ata AP(I) for AI Studio. The EDI acts as a contract between decentralized data
        sources and AI Studio. The EDI is implemented by the data sources, allowing them to be integrated into AI
        Studio later. This means that the data sources assume the server role and AI Studio assumes the client role
        of the API. This approach serves to realize a Retrieval-Augmented Generation (RAG) process with external
        data.
        """;
    
    protected override string SystemPrompt => 
        $"""
         
         """;
    
    protected override IReadOnlyList<IButtonData> FooterButtons => [];
    
    protected override string SubmitText => "Create the EDI server";

    protected override Func<Task> SubmitAction => this.GenerateServer;
    
    protected override ChatThread ConvertToChatThread => (this.chatThread ?? new()) with
    {
        SystemPrompt = SystemPrompts.DEFAULT,
    };
    
    protected override void ResetFrom()
    {
        if (!this.MightPreselectValues())
        {
            this.selectedProgrammingLanguage = ProgrammingLanguages.NONE;
            this.otherProgrammingLanguage = string.Empty;
            this.selectedDataSource = DataSources.NONE;
            this.dataSourceProductName = string.Empty;
            this.otherDataSource = string.Empty;
            this.dataSourceHostname = string.Empty;
            this.dataSourcePort = null;
            this.selectedAuthenticationMethods = [];
            this.authDescription = string.Empty;
            this.selectedOperatingSystem = OperatingSystem.NONE;
            this.retrievalDescription = string.Empty;
            this.additionalLibraries = string.Empty;
        }
    }
    
    protected override bool MightPreselectValues()
    {
        if (this.SettingsManager.ConfigurationData.EDI.PreselectOptions)
        {
            this.selectedProgrammingLanguage = this.SettingsManager.ConfigurationData.EDI.PreselectedProgrammingLanguage;
            this.otherProgrammingLanguage = this.SettingsManager.ConfigurationData.EDI.PreselectedOtherProgrammingLanguage;
            this.selectedDataSource = this.SettingsManager.ConfigurationData.EDI.PreselectedDataSource;
            this.dataSourceProductName = this.SettingsManager.ConfigurationData.EDI.PreselectedDataSourceProductName;
            this.otherDataSource = this.SettingsManager.ConfigurationData.EDI.PreselectedOtherDataSource;
            this.dataSourceHostname = this.SettingsManager.ConfigurationData.EDI.PreselectedDataSourceHostname;
            this.dataSourcePort = this.SettingsManager.ConfigurationData.EDI.PreselectedDataSourcePort;

            var authMethods = new HashSet<Auth>(this.SettingsManager.ConfigurationData.EDI.PreselectedAuthMethods);
            this.selectedAuthenticationMethods = authMethods;
            
            this.authDescription = this.SettingsManager.ConfigurationData.EDI.PreselectedAuthDescription;
            this.selectedOperatingSystem = this.SettingsManager.ConfigurationData.EDI.PreselectedOperatingSystem;
            this.retrievalDescription = this.SettingsManager.ConfigurationData.EDI.PreselectedRetrievalDescription;
            this.additionalLibraries = this.SettingsManager.ConfigurationData.EDI.PreselectedAdditionalLibraries;
            return true;
        }
        
        return false;
    }
    
    private ProgrammingLanguages selectedProgrammingLanguage = ProgrammingLanguages.NONE;
    private string otherProgrammingLanguage = string.Empty;
    private DataSources selectedDataSource = DataSources.NONE;
    private string otherDataSource = string.Empty;
    private string dataSourceProductName = string.Empty;
    private string dataSourceHostname = string.Empty;
    private int? dataSourcePort;
    private IEnumerable<Auth> selectedAuthenticationMethods = new HashSet<Auth>();
    private string authDescription = string.Empty;
    private OperatingSystem selectedOperatingSystem = OperatingSystem.NONE;
    private string retrievalDescription = string.Empty;
    private string additionalLibraries = string.Empty;
    
    private string? ValidateProgrammingLanguage(ProgrammingLanguages language)
    {
        if (language == ProgrammingLanguages.OTHER)
            return null;
        
        if (language == ProgrammingLanguages.NONE)
            return "Please select a programming language for the EDI server.";
        
        return null;
    }
    
    private string? ValidateOtherLanguage(string language)
    {
        if(this.selectedProgrammingLanguage != ProgrammingLanguages.OTHER)
            return null;
        
        if(string.IsNullOrWhiteSpace(language))
            return "Please specify the custom programming language for the EDI server.";
        
        return null;
    }
    
    private string? ValidateDataSource(DataSources dataSource)
    {
        if (dataSource == DataSources.CUSTOM)
            return null;
        
        if (dataSource == DataSources.NONE)
            return "Please select a data source for the EDI server.";
        
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
            return "Please describe the data source of your EDI server.";
        
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
            return "Please provide the hostname of the data source. Use 'localhost' if the data source is on the same machine as the EDI server.";
        
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
    
    private void DataSourceWasChanged()
    {
        if(this.SettingsManager.ConfigurationData.EDI.PreselectedDataSourcePort is not null)
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
            return "Please select at least one authentication method for the EDI server.";
        
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
            return "Please select the operating system on which the EDI server will run. This is necessary when using SSO with Kerberos.";
        
        return null;
    }
    
    private string AuthDescriptionTitle()
    {
        const string TITLE = "Describe how you planned the authentication process";
        return this.IsAuthDescriptionOptional() ? $"(Optional) {TITLE}" : TITLE;
    }

    private bool IsAuthDescriptionOptional()
    {
        var authenticationMethods = (this.selectedAuthenticationMethods as HashSet<Auth>)!;
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
    
    private string? ValidateRetrievalDescription(string description)
    {
        if(string.IsNullOrWhiteSpace(description))
            return "Please describe how the data retrieval process should work. This is important for the integration of the data source into AI Studio by means of the EDI.";
        
        return null;
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

    private async Task GenerateServer()
    {
        await this.form!.Validate();
        if (!this.inputIsValid)
            return;
    }
}