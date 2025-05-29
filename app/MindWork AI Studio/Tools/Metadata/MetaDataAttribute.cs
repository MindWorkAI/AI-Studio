// ReSharper disable ClassNeverInstantiated.Global
namespace AIStudio.Tools.Metadata;

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
    public string BuildTime => buildTime;
    
    public string Version => version;
    
    public uint BuildNum => buildNum;
    
    public string DotnetVersion => dotnetVersion;
    
    public string DotnetSdkVersion => dotnetSdkVersion;
    
    public string RustVersion => rustVersion;
    
    public string MudBlazorVersion => mudBlazorVersion;
    
    public string TauriVersion => tauriVersion;
    
    public string AppCommitHash => appCommitHash;
}