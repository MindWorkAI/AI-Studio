namespace AIStudio.Provider;

/// <summary>
/// The data model for the model to use.
/// </summary>
/// <param name="Id">The model's ID.</param>
public readonly record struct Model(string Id)
{
    #region Overrides of ValueType

    public override string ToString() => string.IsNullOrWhiteSpace(this.Id) ? "no model selected" : this.Id;

    #endregion
}