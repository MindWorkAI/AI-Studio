namespace AIStudio.Tools.PluginSystem;

/// <summary>
/// A plugin which contributes live content, and therefore takes part in deciding a collision.
/// </summary>
/// <remarks>
/// Two plugins may well say something about the same thing. Which of them is heard is decided the
/// same way for every kind of content: a plugin acting on behalf of the organization wins, and
/// among plugins of the same origin the declared priority does. Where the plugin was stored is
/// known from its path; what it declared has to come from the plugin itself, which is all this
/// interface is for.
/// </remarks>
public interface ILivePluginContentSource
{
    /// <summary>
    /// The priority this plugin declares. Zero when it declares none.
    /// </summary>
    public int Priority { get; }
}