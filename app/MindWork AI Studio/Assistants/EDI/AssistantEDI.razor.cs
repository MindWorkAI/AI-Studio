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

    protected override Func<Task> SubmitAction => () => Task.CompletedTask;
    
    protected override ChatThread ConvertToChatThread => (this.chatThread ?? new()) with
    {
        SystemPrompt = SystemPrompts.DEFAULT,
    };
    
    protected override void ResetFrom()
    {
    }
    
    protected override bool MightPreselectValues()
    {
        return false;
    }
    
    private ProgrammingLanguages selectedProgrammingLanguage = ProgrammingLanguages.NONE;
    private string otherProgrammingLanguage = string.Empty;
    private DataSources selectedDataSource = DataSources.NONE;
    private string otherDataSource = string.Empty;
    private IEnumerable<Auth> selectedAuthenticationMethods = new HashSet<Auth>();
    private string authDescription = string.Empty;
    
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
    
    private string? ValidateOtherDataSource(string dataSource)
    {
        if(this.selectedDataSource != DataSources.CUSTOM)
            return null;
        
        if(string.IsNullOrWhiteSpace(dataSource))
            return "Please describe the data source of your EDI server.";
        
        return null;
    }
    
    private string? ValidateAuthenticationMethods(Auth _)
    {
        var authenticationMethods = (this.selectedAuthenticationMethods as HashSet<Auth>)!;
        if(authenticationMethods.Count == 0)
            return "Please select at least one authentication method for the EDI server.";
        
        return null;
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
}