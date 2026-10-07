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
    public bool IsSystemModel => string.Equals(this.Id, SYSTEM_MODEL_ID, StringComparison.Ordinal);

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

    #region Implementation of IEquatable<Model>

    /// <summary>
    /// Two models are the same model when they carry the same ID.
    /// </summary>
    /// <remarks>
    /// The display name is decoration. A provider may report a model under a display name of its own,
    /// while we know the very same model as a hardcoded fallback under a different one. Comparing the
    /// ID alone keeps those two the same model, so that removing duplicates works.
    ///
    /// Note that this overload is the one the runtime uses, for example for Distinct(). The overload
    /// taking a nullable model below is a separate one and never gets called on its behalf, which is
    /// why the hash code has to follow this one.
    /// </remarks>
    /// <param name="other">The model to compare with.</param>
    /// <returns>True, when both models carry the same ID.</returns>
    public bool Equals(Model other) => string.Equals(this.Id, other.Id, StringComparison.Ordinal);

    /// <summary>
    /// Two models are the same model when they carry the same ID.
    /// </summary>
    /// <param name="other">The model to compare with, which may be null.</param>
    /// <returns>True, when the other model exists and carries the same ID.</returns>
    public bool Equals(Model? other) => other is not null && this.Equals(other.Value);

    /// <inheritdoc />
    public override int GetHashCode() => this.Id?.GetHashCode(StringComparison.Ordinal) ?? 0;

    #endregion
}