namespace AIStudio.Tools.PluginSystem;

/// <summary>
/// Checks if a plugin is forbidden.
/// </summary>
public static class ForbiddenPlugins
{
    private const string ID_PATTERN = "ID = \"";
    private static readonly int ID_PATTERN_LEN = ID_PATTERN.Length;
    
    /// <summary>
    /// Checks if the given code represents a forbidden plugin.
    /// </summary>
    /// <param name="code">The code to check.</param>
    /// <returns>The result of the check.</returns>
    public static PluginCheckResult Check(ReadOnlySpan<char> code)
    {
        var endIndex = 0;
        var foundAnyId = false;
        var id = ReadOnlySpan<char>.Empty;
        while (true)
        {
            // Create a slice of the code starting at the end index.
            // This way we can search for all IDs in the code:
            code = code[endIndex..];
            
            // Read the next ID as a string:
            if (!TryGetId(code, out id, out endIndex))
            {
                // When no ID was found at all, we block this plugin.
                // When another ID was found previously, we allow this plugin.
                if(foundAnyId)
                    return new PluginCheckResult(false, null);
                
                return new PluginCheckResult(true, "No ID was found.");
            }
            
            // Try to parse the ID as a GUID:
            if (!Guid.TryParse(id, out var parsedGuid))
            {
                // Again, when no ID was found at all, we block this plugin.
                if(foundAnyId)
                    return new PluginCheckResult(false, null);
                
                return new PluginCheckResult(true, "The ID is not a valid GUID.");
            }

            // Check if the GUID is forbidden:
            if (FORBIDDEN_PLUGINS.TryGetValue(parsedGuid, out var reason))
                return new PluginCheckResult(true, reason);
            
            foundAnyId = true;
        }
    }
    
    private static bool TryGetId(ReadOnlySpan<char> code, out ReadOnlySpan<char> id, out int endIndex)
    {
        //
        // Please note: the code variable is a slice of the original code.
        // That means the indices are relative to the slice, not the original code.
        //
        
        id = ReadOnlySpan<char>.Empty;
        endIndex = 0;
        
        // Find the next ID:
        var idStartIndex = code.IndexOf(ID_PATTERN);
        if (idStartIndex < 0)
            return false;
        
        // Find the start index of the value (Guid):
        var valueStartIndex = idStartIndex + ID_PATTERN_LEN;
        
        // Find the end index of the value. In order to do that,
        // we create a slice of the code starting at the value
        // start index. That means that the end index is relative
        // to the inner slice, not the original code nor the outer slice.
        var valueEndIndex = code[valueStartIndex..].IndexOf('"');
        if (valueEndIndex < 0)
            return false;
        
        // From the perspective of the start index is the end index
        // the length of the value:
        endIndex = valueStartIndex + valueEndIndex;
        id = code.Slice(valueStartIndex, valueEndIndex);
        return true;
    }
    
    /// <summary>
    /// The forbidden plugins.
    /// </summary>
    /// <remarks>
    /// A dictionary that maps the GUID of a plugin to the reason why it is forbidden.
    /// </remarks>
    // ReSharper disable once CollectionNeverUpdated.Local
    private static readonly Dictionary<Guid, string> FORBIDDEN_PLUGINS =
    [
    ];
}