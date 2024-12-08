namespace AIStudio.Assistants.EDI;

public static class AuthExtensions
{
    public static string Name(this Auth auth) => auth switch
    {
        Auth.NONE => "No login necessary: useful for public data sources",
        
        Auth.KERBEROS => "Login by single-sign-on (SSO) using Kerberos: very complex to implement and to operate, useful for many users",
        Auth.USERNAME_PASSWORD => "Login by username and password: simple to implement and to operate, useful for few users; easy to use for users",
        Auth.TOKEN => "Login by token: simple to implement and to operate, useful for few users; unusual for many users",
        
        _ => "Unknown login method"
    };
}