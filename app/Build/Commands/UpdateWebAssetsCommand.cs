// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

using SharedTools;

namespace Build.Commands;

public sealed class UpdateWebAssetsCommand
{
    /// <summary>
    /// The pre-compressed copies and the source maps of the web assets. The app serves its web assets
    /// from the files embedded into it and uses neither, so they would only enlarge it.
    /// </summary>
    private static readonly HashSet<string> SKIPPED_EXTENSIONS = new(StringComparer.OrdinalIgnoreCase) { ".br", ".gz", ".map" };

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
        
        //
        // The destination also holds files of our own, such as the code editor. Hence, we replace
        // only the folder of each package, so that files a package no longer ships disappear:
        //
        var destinationPath = Path.Join(cwd, "wwwroot", "system");
        var counter = 0;
        foreach (var packagePath in Directory.EnumerateDirectories(contentPath))
        {
            var packageDestinationPath = Path.Join(destinationPath, Path.GetFileName(packagePath));
            if (Directory.Exists(packageDestinationPath))
                Directory.Delete(packageDestinationPath, true);

            foreach (var sourcePath in Directory.EnumerateFiles(packagePath, "*", SearchOption.AllDirectories))
            {
                if (SKIPPED_EXTENSIONS.Contains(Path.GetExtension(sourcePath)))
                    continue;

                counter++;
                var targetPath = Path.Join(packageDestinationPath, Path.GetRelativePath(packagePath, sourcePath));
                var targetDirectory = Path.GetDirectoryName(targetPath);
                if (targetDirectory != null)
                    Directory.CreateDirectory(targetDirectory);

                File.Copy(sourcePath, targetPath, true);
            }
        }

        Console.WriteLine($" {counter:###,###} web assets updated successfully.");
        Console.WriteLine();
    }
}