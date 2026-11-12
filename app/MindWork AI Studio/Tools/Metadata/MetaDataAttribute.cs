// ReSharper disable ClassNeverInstantiated.Global
namespace AIStudio.Tools.Metadata;

[AttributeUsage(AttributeTargets.Assembly)]
public class MetaDataAttribute(
    string version,
    string buildTime,
    uint buildNum,
    string dotnetSdkVersion,
    string mudBlazorVersion,
    string tauriVersion,
    string appCommitHash
    ) : Attribute
{
    public string BuildTime => buildTime;
    
    public string Version => version;
    
    public uint BuildNum => buildNum;
    
    /// <summary>
    /// Gets the .NET SDK which built the app. The .NET runtime which runs it is <see cref="DotnetRuntimeVersion.Current"/>,
    /// and the Rust compiler which built the runtime is <see cref="Rust.RuntimeInfoResponse.RustVersion"/>.
    /// </summary>
    public string DotnetSdkVersion => dotnetSdkVersion;

    public string MudBlazorVersion => mudBlazorVersion;
    
    public string TauriVersion => tauriVersion;
    
    public string AppCommitHash => appCommitHash;
}