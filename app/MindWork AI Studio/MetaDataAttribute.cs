namespace AIStudio;

[AttributeUsage(AttributeTargets.Assembly)]
public class MetaDataAttribute(
    string version,
    string buildTime,
    uint buildNum,
    string dotnetSdkVersion,
    string dotnetVersion,
    string rustVersion,
    string mudBlazorVersion,
    string tauriVersion,
    string appCommitHash
    ) : Attribute
{
    public string BuildTime { get; } = buildTime;
    
    public string Version { get; } = version;
    
    public uint BuildNum { get; } = buildNum;
    
    public string DotnetVersion { get; } = dotnetVersion;
    
    public string DotnetSdkVersion { get; } = dotnetSdkVersion;
    
    public string RustVersion { get; } = rustVersion;
    
    public string MudBlazorVersion { get; } = mudBlazorVersion;
    
    public string TauriVersion { get; } = tauriVersion;
    
    public string AppCommitHash { get; } = appCommitHash;
}