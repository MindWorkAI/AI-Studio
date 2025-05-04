using AIStudio.Tools.ERIClient.DataModel;
using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools;

public static class AuthMethodsV1Extensions
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(AuthMethodsV1Extensions).Namespace, nameof(AuthMethodsV1Extensions));
    
    public static string DisplayName(this AuthMethod authMethod) => authMethod switch
    {
        AuthMethod.NONE => TB("None"),
        AuthMethod.USERNAME_PASSWORD => TB("Username & Password"),
        AuthMethod.KERBEROS => TB("SSO (Kerberos)"),
        AuthMethod.TOKEN => TB("Access Token"),
        
        _ => TB("Unknown authentication method"),
    };
}