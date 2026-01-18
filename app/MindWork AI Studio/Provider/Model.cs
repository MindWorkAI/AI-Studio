using AIStudio.Tools.PluginSystem;

namespace AIStudio.Provider;

/// <summary>
/// The data model for the model to use.
/// </summary>
/// <param name="Id">The model's ID.</param>
/// <param name="DisplayName">The model's display name.</param>
public readonly record struct Model(string Id, string? DisplayName)
{
    /// <summary>
    /// Special model ID used when the model is selected by the system/host
    /// and cannot be changed by the user (e.g., llama.cpp, whisper.cpp).
    /// </summary>
    private const string SYSTEM_MODEL_ID = "::system::";

    /// <summary>
    /// Creates a system-configured model placeholder.
    /// </summary>
    public static readonly Model SYSTEM_MODEL = new(SYSTEM_MODEL_ID, null);

    /// <summary>
    /// Checks if this model is the system-configured placeholder.
    /// </summary>
    public bool IsSystemModel => this == SYSTEM_MODEL;

    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(Model).Namespace, nameof(Model));
    
    #region Overrides of ValueType

    public override string ToString()
    {
        if(!string.IsNullOrWhiteSpace(this.DisplayName))
            return this.DisplayName;
        
        if(!string.IsNullOrWhiteSpace(this.Id))
            return this.Id;
        
        return TB("no model selected");
    }

    #endregion

    #region Implementation of IEquatable<Model?>

    public bool Equals(Model? other)
    {
        if(other is null)
            return false;
        
        return this.Id == other.Value.Id;
    }

    #endregion
}