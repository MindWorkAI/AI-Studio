namespace AIStudio.Assistants.ERI;

public static class ERIVersionExtensions
{
    extension(ERIVersion version)
    {
        public Task<string> ReadSpecification(WebRootFiles webRootFiles) => webRootFiles.ReadTextAsync(version.SpecificationURL());

        public string SpecificationURL()
        {
            var nameLower = version.ToString().ToLowerInvariant();
            var filename = $"{nameLower}.json";
            return $"specs/eri/{filename}";
        }

        public bool WasSpecificationSelected() => version != ERIVersion.NONE;
    }
}