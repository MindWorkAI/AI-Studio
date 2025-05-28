namespace AIStudio.Assistants.ERI;

public static class AuthExtensions
{
    private static string TB(string fallbackEN) => Tools.PluginSystem.I18N.I.T(fallbackEN, typeof(AuthExtensions).Namespace, nameof(AuthExtensions));
    
    public static string Name(this Auth auth) => auth switch
    {
        Auth.NONE => TB("No login necessary: useful for public data sources"),
        
        Auth.KERBEROS => TB("Login by single-sign-on (SSO) using Kerberos: very complex to implement and to operate, useful for many users"),
        Auth.USERNAME_PASSWORD => TB("Login by username and password: simple to implement and to operate, useful for few users; easy to use for users"),
        Auth.TOKEN => TB("Login by token: simple to implement and to operate, useful for few users; unusual for many users"),
        
        _ => TB("Unknown login method")
    };
    
    public static string ToPrompt(this Auth auth) => auth switch
    {
        Auth.NONE => "No login is necessary, the data source is public.",
        
        Auth.KERBEROS => "Login by single-sign-on (SSO) using Kerberos.",
        Auth.USERNAME_PASSWORD => "Login by username and password.",
        Auth.TOKEN => "Login by static token per user.",
        
        _ => string.Empty,
    };
}