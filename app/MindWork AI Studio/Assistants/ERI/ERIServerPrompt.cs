using System.Text;

namespace AIStudio.Assistants.ERI;

/// <summary>
/// What the ERI Server assistant tells the model about the server it has to implement.
/// </summary>
/// <remarks>
/// Kept apart from the assistant, so that the system prompt can be checked without a running app:
/// every input of the form has to arrive in it, and in a shape the model can read.
/// </remarks>
internal sealed class ERIServerPrompt
{
    public ProgrammingLanguages ProgrammingLanguage { get; init; } = ProgrammingLanguages.NONE;

    public string OtherProgrammingLanguage { get; init; } = string.Empty;

    /// <summary>
    /// The OpenAPI description of the ERI, or null while it is not loaded yet.
    /// </summary>
    public string? ERISpecification { get; init; }

    public string ServerName { get; init; } = string.Empty;

    public string ServerDescription { get; init; } = string.Empty;

    public DataSources DataSource { get; init; } = DataSources.NONE;

    public string OtherDataSource { get; init; } = string.Empty;

    public string DataSourceProductName { get; init; } = string.Empty;

    public string DataSourceHostname { get; init; } = string.Empty;

    public int? DataSourcePort { get; init; }

    public IEnumerable<Auth> AuthenticationMethods { get; init; } = [];

    /// <summary>
    /// How the authentication methods are meant to be used, e.g., which data is public.
    /// </summary>
    public string AuthDescription { get; init; } = string.Empty;

    public OperatingSystem OperatingSystem { get; init; } = OperatingSystem.NONE;

    public AllowedLLMProviders AllowedLLMProviders { get; init; } = AllowedLLMProviders.NONE;

    public IReadOnlyList<RetrievalInfo> RetrievalProcesses { get; init; } = [];

    public string AdditionalLibraries { get; init; } = string.Empty;

    public string ToSystemPrompt()
    {
        var sb = new StringBuilder();

        //
        // ---------------------------------
        // Introduction
        // ---------------------------------
        //
        var programmingLanguage = this.ProgrammingLanguage is ProgrammingLanguages.OTHER ? this.OtherProgrammingLanguage : this.ProgrammingLanguage.ToPrompt();
        sb.Append($"""
                   # Introduction
                   You are an experienced {programmingLanguage} developer. Your task is to implement an API server in
                   the {programmingLanguage} language according to the following OpenAPI Description (OAD):

                   ```
                   {this.ERISpecification}
                   ```

                   The server realizes the data retrieval component for a "Retrieval-Augmentation Generation" (RAG) process.
                   The server is called "{this.ServerName}" and is described as follows:

                   ```
                   {this.ServerDescription}
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

        switch (this.DataSource)
        {
            case DataSources.CUSTOM:
                sb.Append($"""

                           The data source for the retrieval process is described as follows:

                           ```
                           {this.OtherDataSource}
                           ```
                           """);

                if(!string.IsNullOrWhiteSpace(this.DataSourceHostname))
                {
                    sb.Append($"""

                               The data source is accessible via the hostname `{this.DataSourceHostname}`.
                               """);
                }

                if(this.DataSourcePort is not null)
                {
                    sb.Append($"""

                               The data source is accessible via port `{this.DataSourcePort}`.
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

                           The data source for the retrieval process is an "{this.DataSourceProductName}" database running on the
                           host `{this.DataSourceHostname}` and is accessible via port `{this.DataSourcePort}`.
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

        foreach (var auth in this.AuthenticationMethods)
        {
            sb.Append($"""

                       - {auth.ToPrompt()}
                       """);
        }

        if(!string.IsNullOrWhiteSpace(this.AuthDescription))
        {
            sb.Append($"""

                       The authentication process is planned as follows:

                       ```
                       {this.AuthDescription}
                       ```
                       """);
        }

        if(this.AuthenticationMethods.Contains(Auth.KERBEROS))
        {
            sb.Append($"""

                       The server will run on {this.OperatingSystem.ToPrompt()} operating systems. Keep
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
                  The following security requirement for `allowedProviderType` was chosen: `{this.AllowedLLMProviders}`
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
        foreach (var retrievalProcess in this.RetrievalProcesses)
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

        if (!string.IsNullOrWhiteSpace(this.AdditionalLibraries))
        {
            sb.Append($"""

                       # Additional Libraries
                       You use the following libraries for your implementation:

                       {this.AdditionalLibraries}
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
                  - Important: The JSON objects of the API messages use camel case for the data field names.
                  """);

        return sb.ToString();
    }
}