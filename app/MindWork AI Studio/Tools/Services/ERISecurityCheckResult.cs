using AIStudio.Tools.ERIClient.DataModel;

namespace AIStudio.Tools.Services;

/// <summary>
/// What the server of an ERI data source answered to the check of its security requirements.
/// </summary>
/// <param name="IsReachable">Whether the server answered and the sign-in succeeded.</param>
/// <param name="Requirements">What the server requires, or null when it could not be reached.</param>
internal readonly record struct ERISecurityCheckResult(bool IsReachable, SecurityRequirements? Requirements)
{
    /// <summary>
    /// The server could not be reached, or it refused the sign-in.
    /// </summary>
    public static readonly ERISecurityCheckResult UNREACHABLE = new(false, null);
}