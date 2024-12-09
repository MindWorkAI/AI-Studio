namespace AIStudio.Assistants.EDI;

public static class EDIVersionExtensions
{
    public static async Task<string> ReadSpecification(this EDIVersion version, HttpClient httpClient)
    {
        try
        {
            var url = version.SpecificationURL();
            var response = await httpClient.GetAsync(url);
            return await response.Content.ReadAsStringAsync();
        }
        catch
        {
            return string.Empty;
        }
    }
    
    public static string SpecificationURL(this EDIVersion version)
    {
        var nameLower = version.ToString().ToLowerInvariant();
        var filename = $"{nameLower}.json";
        return $"specs/edi/{filename}";
    }
    
    public static bool WasSpecificationSelected(this EDIVersion version) => version != EDIVersion.NONE;
}