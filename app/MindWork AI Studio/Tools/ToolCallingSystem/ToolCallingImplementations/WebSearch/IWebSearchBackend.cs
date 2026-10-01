namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.WebSearch;

/// <summary>
/// One search service the web search tool can ask.
/// </summary>
/// <remarks>
/// A backend owns everything about itself: which settings it needs, what they are called in
/// the user's language, where to get an account for it, whether it has been configured, and
/// how to turn a search into its own API call. Adding one is therefore a new class, a line
/// in the dependency injection setup, and a member in the backend enum — the tool itself
/// stays as it is.<br/><br/>
/// Settings are shared with the tool through one flat dictionary, so a backend prefixes its
/// field names with its own settings group. That keeps two backends asking for an API key
/// apart, and it keeps an organization's configuration readable.
/// </remarks>
public interface IWebSearchBackend
{
    public WebSearchBackend Backend { get; }

    /// <summary>
    /// The settings group holding this backend's fields.
    /// </summary>
    /// <remarks>
    /// The group is how the tool decides which backend a field belongs to, so it is also the
    /// prefix every field name of this backend carries.
    /// </remarks>
    public string SettingsGroup { get; }

    /// <summary>
    /// What this backend can do with the parts of a search besides the query.
    /// </summary>
    /// <remarks>
    /// Read before the search rather than reported after it, because some of it decides
    /// whether this backend is asked for a particular search at all.
    /// </remarks>
    public WebSearchCapabilities Capabilities { get; }

    /// <summary>
    /// Adds this backend's settings fields to the tool's schema.
    /// </summary>
    /// <remarks>
    /// None of them may be required: a user who configured another backend must still be able
    /// to save the tool's settings. That at least one backend is configured is checked by the
    /// tool instead.
    /// </remarks>
    public void DeclareSettings(ToolSettingsSchemaBuilder builder);

    public string GetSettingsGroupLabel();

    public IReadOnlyList<ToolSettingsGroupLink> GetSettingsGroupLinks();

    public string GetSettingsFieldLabel(string fieldName);

    public string GetSettingsFieldDescription(string fieldName);

    public string? GetSettingsFieldDefaultValue(string fieldName);

    /// <summary>
    /// Whether the user filled in what this backend needs to be asked at all.
    /// </summary>
    public bool IsConfigured(IReadOnlyDictionary<string, string> settingsValues);

    /// <summary>
    /// Checks the settings of a configured backend and says what is wrong with them.
    /// </summary>
    /// <remarks>
    /// Only called for a backend that counts as configured, so it does not have to repeat the
    /// checks that decide that.
    /// </remarks>
    public bool TryValidateConfiguration(IReadOnlyDictionary<string, string> settingsValues, out string error);

    /// <summary>
    /// Runs one search.
    /// </summary>
    /// <remarks>
    /// Failures are thrown, with the reason in the message: it reaches the user through the
    /// tool trace and the model through the tool result, and neither can act on "it failed".
    /// Returning no hits is not a failure, and a backend that could not honour a part of the
    /// query says so through the notes of its result rather than by throwing.
    /// </remarks>
    public Task<WebSearchBackendResult> SearchAsync(WebSearchQuery query, IReadOnlyDictionary<string, string> settingsValues, CancellationToken token = default);
}