namespace Build.Tools;

public static class RIDExtensions
{
    public static string ToName(this RID rid) => rid switch
    {
        RID.WIN_X64 => "win-x64",
        RID.WIN_ARM64 => "win-arm64",
        
        RID.LINUX_X64 => "linux-x64",
        RID.LINUX_ARM64 => "linux-arm64",
        
        RID.OSX_X64 => "osx-x64",
        RID.OSX_ARM64 => "osx-arm64",
        
        _ => string.Empty,
    };
}