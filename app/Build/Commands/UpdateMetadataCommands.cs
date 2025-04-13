namespace Build.Commands;

// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

public sealed class UpdateMetadataCommands
{
    [Command("test", Description = "Test command")]
    public async Task Test()
    {
        await this.UpdateBuildTime();
    }
    
    private async Task UpdateBuildTime()
    {
        var pathMetadata = Environment.GetMetadataPath();
        var lines = await File.ReadAllLinesAsync(pathMetadata, Encoding.UTF8);
        var buildTime = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + " UTC";

        Console.WriteLine($"- Updating build time from '{lines[1]}' to '{buildTime}'.");
        
        lines[1] = buildTime;
        await File.WriteAllLinesAsync(pathMetadata, lines, Encoding.UTF8);
    }
}