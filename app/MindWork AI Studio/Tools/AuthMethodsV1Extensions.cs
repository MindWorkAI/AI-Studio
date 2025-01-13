using ERI_Client.V1;

namespace AIStudio.Tools;

public static class AuthMethodsV1Extensions
{
    public static string DisplayName(this AuthMethod authMethod) => authMethod switch
    {
        AuthMethod.NONE => "None",
        AuthMethod.USERNAME_PASSWORD => "Username & Password",
        AuthMethod.KERBEROS => "SSO (Kerberos)",
        AuthMethod.TOKEN => "Access Token",
        
        _ => "Unknown authentication method",
    };
}