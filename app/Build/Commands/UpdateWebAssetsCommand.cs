// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

using Build.Tools;

namespace Build.Commands;

public sealed class UpdateWebAssetsCommand
{
    [Command("update-web", Description = "Update web assets")]
    public void UpdateWebAssets()
    {
        if(!Environment.IsWorkingDirectoryValid())
            return;
        
        var rid = Environment.GetRidsForCurrentOS().First();
        var cwd = Environment.GetAIStudioDirectory();
        var contentPath = Path.Join(cwd, "bin", "release", Environment.DOTNET_VERSION, rid.ToName(), "publish", "wwwroot", "_content");
        var isMudBlazorDirectoryPresent = Directory.Exists(Path.Join(contentPath, "MudBlazor"));
        if (!isMudBlazorDirectoryPresent)
        {
            Console.WriteLine($"No web assets found for RID '{rid}'. Please publish the project first.");
            return;
        }
        
        Directory.CreateDirectory(Path.Join(cwd, "wwwroot", "system"));
        var sourcePaths = Directory.EnumerateFiles(contentPath, "*", SearchOption.AllDirectories);
        var counter = 0;
        foreach(var sourcePath in sourcePaths)
        {
            counter++;
            var relativePath = Path.GetRelativePath(cwd, sourcePath);
            var targetPath = Path.Join(cwd, "wwwroot", relativePath);
            var targetDirectory = Path.GetDirectoryName(targetPath);
            if (targetDirectory != null)
                Directory.CreateDirectory(targetDirectory);
            
            File.Copy(sourcePath, targetPath, true);
        }
        
        Console.WriteLine($"{counter:###,###} web assets updated successfully.");
    }
}