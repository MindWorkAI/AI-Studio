namespace AIStudio.Tools.ToolCallingSystem;

public static class SafeSearchPolicyExtensions
{
    /// <summary>
    /// The value SearXNG expects for its safesearch parameter.
    /// </summary>
    /// <remarks>
    /// SearXNG takes the level as a number. That number stays here, at the edge towards the
    /// search engine, instead of travelling through the settings where nobody can read it.
    /// </remarks>
    public static string ToSearXNGValue(this SafeSearchPolicy policy) => policy switch
    {
        SafeSearchPolicy.OFF => "0",
        SafeSearchPolicy.MODERATE => "1",
        SafeSearchPolicy.STRICT => "2",

        _ => "0",
    };
}