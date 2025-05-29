using AIStudio.Tools.PluginSystem;

namespace AIStudio.Provider;

/// <summary>
/// The data model for the model to use.
/// </summary>
/// <param name="Id">The model's ID.</param>
/// <param name="DisplayName">The model's display name.</param>
public readonly record struct Model(string Id, string? DisplayName)
{
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