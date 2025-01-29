namespace AIStudio.Assistants.ERI;

public static class ERIVersionExtensions
{
    public static async Task<string> ReadSpecification(this ERIVersion version, HttpClient httpClient)
    {
        try
        {
            var url = version.SpecificationURL();
            using var response = await httpClient.GetAsync(url);
            return await response.Content.ReadAsStringAsync();
        }
        catch
        {
            return string.Empty;
        }
    }
    
    public static string SpecificationURL(this ERIVersion version)
    {
        var nameLower = version.ToString().ToLowerInvariant();
        var filename = $"{nameLower}.json";
        return $"specs/eri/{filename}";
    }
    
    public static bool WasSpecificationSelected(this ERIVersion version) => version != ERIVersion.NONE;
}