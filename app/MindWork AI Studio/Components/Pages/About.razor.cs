using System.Reflection;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components.Pages;

public partial class About : ComponentBase
{
    private static readonly Assembly ASSEMBLY = Assembly.GetExecutingAssembly();
    private static readonly MetaDataAttribute META_DATA = ASSEMBLY.GetCustomAttribute<MetaDataAttribute>()!;

    private static string VersionDotnet => $"Used .NET compiler: v{META_DATA.DotnetVersion}";
    
    private static string VersionDotnetSdk => $"Used .NET SDK: v{META_DATA.DotnetSdkVersion}";
    
    private static string VersionRust => $"Used Rust compiler: v{META_DATA.RustVersion}";

    private static string VersionApp => $"MindWork AI Studio: v{META_DATA.Version} (commit {META_DATA.AppCommitHash}, build {META_DATA.BuildNum})";
    
    private static string BuildTime => $"Build time: {META_DATA.BuildTime}";
    
    private static string MudBlazorVersion => $"MudBlazor: v{META_DATA.MudBlazorVersion}";
    
    private static string TauriVersion => $"Tauri: v{META_DATA.TauriVersion}";
}