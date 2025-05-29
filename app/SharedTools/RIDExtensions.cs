namespace SharedTools;

public static class RIDExtensions
{
    public static string AsMicrosoftRid(this RID rid) => rid switch
    {
        RID.WIN_X64 => "win-x64",
        RID.WIN_ARM64 => "win-arm64",
        
        RID.LINUX_X64 => "linux-x64",
        RID.LINUX_ARM64 => "linux-arm64",
        
        RID.OSX_X64 => "osx-x64",
        RID.OSX_ARM64 => "osx-arm64",
        
        _ => string.Empty,
    };
    
    public static string ToUserFriendlyName(this RID rid) => rid switch
    {
        RID.WIN_X64 => "Windows x64",
        RID.WIN_ARM64 => "Windows ARM64",
        
        RID.LINUX_X64 => "Linux x64",
        RID.LINUX_ARM64 => "Linux ARM64",
        
        RID.OSX_X64 => "macOS x64",
        RID.OSX_ARM64 => "macOS ARM64",
        
        _ => "unknown",
    };
    
    public static RID ToRID(this string rid) => rid switch
    {
        "win-x64" => RID.WIN_X64,
        "win-arm64" => RID.WIN_ARM64,
        
        "linux-x64" => RID.LINUX_X64,
        "linux-arm64" => RID.LINUX_ARM64,
        
        "osx-x64" => RID.OSX_X64,
        "osx-arm64" => RID.OSX_ARM64,
        
        _ => RID.NONE,
    };
}