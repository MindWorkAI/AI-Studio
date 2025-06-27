// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

using SharedTools;

namespace Build.Commands;

public sealed class UpdateWebAssetsCommand
{
    [Command("update-web", Description = "Update web assets")]
    public void UpdateWebAssets()
    {
        if(!Environment.IsWorkingDirectoryValid())
            return;

        Console.WriteLine("=========================");
        Console.Write("- Updating web assets ...");
        
        var rid = Environment.GetCurrentRid();
        var cwd = Environment.GetAIStudioDirectory();
        var contentPath = Path.Join(cwd, "bin", "release", Environment.DOTNET_VERSION, rid.AsMicrosoftRid(), "publish", "wwwroot", "_content");
        
        var isMudBlazorDirectoryPresent = Directory.Exists(Path.Join(contentPath, "MudBlazor"));
        if (!isMudBlazorDirectoryPresent)
        {
            Console.WriteLine();
            Console.WriteLine($"- Error: No web assets found for RID '{rid}'. Please publish the project first.");
            return;
        }
        
        var destinationPath = Path.Join(cwd, "wwwroot", "system");
        if(Directory.Exists(destinationPath))
            Directory.Delete(destinationPath, true);
        
        Directory.CreateDirectory(destinationPath);
        
        var sourcePaths = Directory.EnumerateFiles(contentPath, "*", SearchOption.AllDirectories);
        var counter = 0;
        foreach(var sourcePath in sourcePaths)
        {
            counter++;
            var relativePath = sourcePath
                .Replace(contentPath, "")
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var targetPath = Path.Join(cwd, "wwwroot", "system", relativePath);
            var targetDirectory = Path.GetDirectoryName(targetPath);
            if (targetDirectory != null)
                Directory.CreateDirectory(targetDirectory);
            
            File.Copy(sourcePath, targetPath, true);
        }

        Console.WriteLine($" {counter:###,###} web assets updated successfully.");
        Console.WriteLine();
    }
}