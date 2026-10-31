namespace AIStudio.Assistants.ERI;

public static class ERIVersionExtensions
{
    public static Task<string> ReadSpecification(this ERIVersion version, WebRootFiles webRootFiles) => webRootFiles.ReadTextAsync(version.SpecificationURL());

    public static string SpecificationURL(this ERIVersion version)
    {
        var nameLower = version.ToString().ToLowerInvariant();
        var filename = $"{nameLower}.json";
        return $"specs/eri/{filename}";
    }

    public static bool WasSpecificationSelected(this ERIVersion version) => version != ERIVersion.NONE;
}