namespace AIStudio.Settings.DataModel;

public enum DataSourceERIUsernamePasswordMode
{
    /// <summary>
    /// The user manages the username and password locally.
    /// </summary>
    USER_MANAGED,

    /// <summary>
    /// The username and password are shared by all users and provided by configuration.
    /// </summary>
    SHARED_USERNAME_AND_PASSWORD,

    /// <summary>
    /// The username is read from the operating system, and the password is shared by all users.
    /// </summary>
    OS_USERNAME_SHARED_PASSWORD,
}