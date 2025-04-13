using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Build.Commands;

// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

public sealed partial class UpdateMetadataCommands
{
    [Command("test", Description = "Test command")]
    public async Task Test()
    {
        await this.UpdateTauriVersion();
    }

    private async Task UpdateLicenceYear(string licenceFilePath)
    {
        var currentYear = DateTime.UtcNow.Year.ToString();
        var lines = await File.ReadAllLinesAsync(licenceFilePath, Encoding.UTF8);

        var found = false;
        var copyrightYear = string.Empty;
        var updatedLines = new List<string>(lines.Length);
        foreach (var line in lines)
        {
            var match = FindCopyrightRegex().Match(line);
            if (match.Success)
            {
                copyrightYear = match.Groups["year"].Value;
                
                if(!found && copyrightYear != currentYear)
                    Console.WriteLine($"- Updating the licence's year in '{Path.GetFileName(licenceFilePath)}' from '{copyrightYear}' to '{currentYear}'.");
                
                updatedLines.Add(ReplaceCopyrightYearRegex().Replace(line, currentYear));
                found = true;
            }
            else
                updatedLines.Add(line);
        }
        
        await File.WriteAllLinesAsync(licenceFilePath, updatedLines, Environment.UTF8_NO_BOM);
        if (!found)
            Console.WriteLine($"- Error: No copyright year found in '{Path.GetFileName(licenceFilePath)}'.");
        else if (copyrightYear == currentYear)
            Console.WriteLine($"- The copyright year in '{Path.GetFileName(licenceFilePath)}' is already up to date.");
    }
    
    private async Task UpdateTauriVersion()
    {
        const int TAURI_VERSION_INDEX = 7;
        
        var pathMetadata = Environment.GetMetadataPath();
        var lines = await File.ReadAllLinesAsync(pathMetadata, Encoding.UTF8);
        var currentTauriVersion = lines[TAURI_VERSION_INDEX].Trim();
        
        var matches = await this.DetermineVersion("Tauri", Environment.GetRustRuntimeDirectory(), TauriVersionRegex(), "cargo", "tree --depth 1");
        if (matches.Count == 0)
            return;
        
        var updatedTauriVersion = matches[0].Groups["version"].Value;
        if(currentTauriVersion == updatedTauriVersion)
        {
            Console.WriteLine("- The Tauri version is already up to date.");
            return;
        }
        
        Console.WriteLine($"- Updated Tauri version from {currentTauriVersion} to {updatedTauriVersion}.");
        lines[TAURI_VERSION_INDEX] = updatedTauriVersion;
        
        await File.WriteAllLinesAsync(pathMetadata, lines, Environment.UTF8_NO_BOM);
    }
    
    private async Task UpdateMudBlazorVersion()
    {
        const int MUD_BLAZOR_VERSION_INDEX = 6;
        
        var pathMetadata = Environment.GetMetadataPath();
        var lines = await File.ReadAllLinesAsync(pathMetadata, Encoding.UTF8);
        var currentMudBlazorVersion = lines[MUD_BLAZOR_VERSION_INDEX].Trim();
        
        var matches = await this.DetermineVersion("MudBlazor", Environment.GetAIStudioDirectory(), MudBlazorVersionRegex(), "dotnet", "list package");
        if (matches.Count == 0)
            return;
        
        var updatedMudBlazorVersion = matches[0].Groups["version"].Value;
        if(currentMudBlazorVersion == updatedMudBlazorVersion)
        {
            Console.WriteLine("- The MudBlazor version is already up to date.");
            return;
        }
        
        Console.WriteLine($"- Updated MudBlazor version from {currentMudBlazorVersion} to {updatedMudBlazorVersion}.");
        lines[MUD_BLAZOR_VERSION_INDEX] = updatedMudBlazorVersion;
        
        await File.WriteAllLinesAsync(pathMetadata, lines, Environment.UTF8_NO_BOM);
    }

    private async Task UpdateRustVersion()
    {
        const int RUST_VERSION_INDEX = 5;
        
        var pathMetadata = Environment.GetMetadataPath();
        var lines = await File.ReadAllLinesAsync(pathMetadata, Encoding.UTF8);
        var currentRustVersion = lines[RUST_VERSION_INDEX].Trim();
        var matches = await this.DetermineVersion("Rust", Environment.GetRustRuntimeDirectory(), RustVersionRegex(), "rustc", "-Vv");
        if (matches.Count == 0)
            return;
        
        var updatedRustVersion = matches[0].Groups["version"].Value + " (commit " + matches[0].Groups["commit"].Value + ")";
        if(currentRustVersion == updatedRustVersion)
        {
            Console.WriteLine("- Rust version is already up to date.");
            return;
        }
        
        Console.WriteLine($"- Updated Rust version from {currentRustVersion} to {updatedRustVersion}.");
        lines[RUST_VERSION_INDEX] = updatedRustVersion;
        
        await File.WriteAllLinesAsync(pathMetadata, lines, Environment.UTF8_NO_BOM);
    }

    private async Task UpdateDotnetVersion()
    {
        const int DOTNET_VERSION_INDEX = 4;
        const int DOTNET_SDK_VERSION_INDEX = 3;
        
        var pathMetadata = Environment.GetMetadataPath();
        var lines = await File.ReadAllLinesAsync(pathMetadata, Encoding.UTF8);
        var currentDotnetVersion = lines[DOTNET_VERSION_INDEX].Trim();
        var currentDotnetSdkVersion = lines[DOTNET_SDK_VERSION_INDEX].Trim();
        
        var matches = await this.DetermineVersion(".NET", Environment.GetAIStudioDirectory(), DotnetVersionRegex(), "dotnet", "--info");
        if (matches.Count == 0)
            return;
        
        var updatedDotnetVersion = matches[0].Groups["hostVersion"].Value + " (commit " + matches[0].Groups["hostCommit"].Value + ")";
        var updatedDotnetSdkVersion = matches[0].Groups["sdkVersion"].Value + " (commit " + matches[0].Groups["sdkCommit"].Value + ")";
        if(currentDotnetVersion == updatedDotnetVersion && currentDotnetSdkVersion == updatedDotnetSdkVersion)
        {
            Console.WriteLine("- .NET version is already up to date.");
            return;
        }
        
        Console.WriteLine($"- Updated .NET SDK version from {currentDotnetSdkVersion} to {updatedDotnetSdkVersion}.");
        Console.WriteLine($"- Updated .NET version from {currentDotnetVersion} to {updatedDotnetVersion}.");

        lines[DOTNET_VERSION_INDEX] = updatedDotnetVersion;
        lines[DOTNET_SDK_VERSION_INDEX] = updatedDotnetSdkVersion;
        
        await File.WriteAllLinesAsync(pathMetadata, lines, Environment.UTF8_NO_BOM);
    }
    
    private async Task<IList<Match>> DetermineVersion(string name, string workingDirectory,  Regex regex, string program, string command)
    {
        var processInfo = new ProcessStartInfo
        {
            WorkingDirectory = workingDirectory,
            FileName = program,
            Arguments = command,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        
        using var process = new Process();
        process.StartInfo = processInfo;
        process.Start();
        
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        
        var matches = regex.Matches(output);
        if (matches.Count == 0)
        {
            Console.WriteLine($"- Error: Was not able to determine the {name} version.");
            return [];
        }
        
        return matches;
    }
    
    private async Task IncreaseBuildNumber()
    {
        const int BUILD_NUMBER_INDEX = 2;
        var pathMetadata = Environment.GetMetadataPath();
        var lines = await File.ReadAllLinesAsync(pathMetadata, Encoding.UTF8);
        var buildNumber = int.Parse(lines[BUILD_NUMBER_INDEX]) + 1;

        Console.WriteLine($"- Updating build number from '{lines[BUILD_NUMBER_INDEX]}' to '{buildNumber}'.");
        
        lines[BUILD_NUMBER_INDEX] = buildNumber.ToString();
        await File.WriteAllLinesAsync(pathMetadata, lines, Environment.UTF8_NO_BOM);
    }
    
    private async Task UpdateBuildTime()
    {
        const int BUILD_TIME_INDEX = 1;
        var pathMetadata = Environment.GetMetadataPath();
        var lines = await File.ReadAllLinesAsync(pathMetadata, Encoding.UTF8);
        var buildTime = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + " UTC";

        Console.WriteLine($"- Updating build time from '{lines[BUILD_TIME_INDEX]}' to '{buildTime}'.");
        
        lines[BUILD_TIME_INDEX] = buildTime;
        await File.WriteAllLinesAsync(pathMetadata, lines, Environment.UTF8_NO_BOM);
    }

    [GeneratedRegex("""(?ms).?(NET\s+SDK|SDK\s+\.NET)\s*:\s+Version:\s+(?<sdkVersion>[0-9.]+).+Commit:\s+(?<sdkCommit>[a-zA-Z0-9]+).+Host:\s+Version:\s+(?<hostVersion>[0-9.]+).+Commit:\s+(?<hostCommit>[a-zA-Z0-9]+)""")]
    private static partial Regex DotnetVersionRegex();
    
    [GeneratedRegex("""rustc (?<version>[0-9.]+)(?:-nightly)? \((?<commit>[a-zA-Z0-9]+)""")]
    private static partial Regex RustVersionRegex();
    
    [GeneratedRegex("""MudBlazor\s+(?<version>[0-9.]+)""")]
    private static partial Regex MudBlazorVersionRegex();
    
    [GeneratedRegex("""tauri\s+v(?<version>[0-9.]+)""")]
    private static partial Regex TauriVersionRegex();
    
    [GeneratedRegex("""^\s*Copyright\s+(?<year>[0-9]{4})""")]
    private static partial Regex FindCopyrightRegex();

    [GeneratedRegex("""([0-9]{4})""")]
    private static partial Regex ReplaceCopyrightYearRegex();
}