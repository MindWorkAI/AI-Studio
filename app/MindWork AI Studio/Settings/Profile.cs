namespace AIStudio.Settings;

public readonly record struct Profile(uint Num, string Id, string Name, string NeedToKnow, string Actions)
{
    #region Overrides of ValueType

    /// <summary>
    /// Returns a string that represents the profile in a human-readable format.
    /// </summary>
    /// <returns>A string that represents the profile in a human-readable format.</returns>
    public override string ToString() => this.Name;

    #endregion
}