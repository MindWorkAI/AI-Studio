// ReSharper disable MemberCanBePrivate.Global
namespace AIStudio.Tools.PluginSystem;

/// <summary>
/// Represents a version number for a plugin.
/// </summary>
/// <param name="Major">The major version number.</param>
/// <param name="Minor">The minor version number.</param>
/// <param name="Patch">The patch version number.</param>
public readonly record struct PluginVersion(int Major, int Minor, int Patch) : IComparable<PluginVersion>
{
    /// <summary>
    /// Represents no version number.
    /// </summary>
    public static readonly PluginVersion NONE = new(0, 0, 0);
    
    /// <summary>
    /// Tries to parse the input string as a plugin version number.
    /// </summary>
    /// <param name="input">The input string to parse.</param>
    /// <param name="version">The parsed version number.</param>
    /// <returns>True when the input string was successfully parsed; otherwise, false.</returns>
    public static bool TryParse(string input, out PluginVersion version)
    {
        try
        {
            version = Parse(input);
            return true;
        }
        catch
        {
            version = NONE;
            return false;
        }
    }
    
    /// <summary>
    /// Parses the input string as a plugin version number.
    /// </summary>
    /// <param name="input">The input string to parse.</param>
    /// <returns>The parsed version number.</returns>
    /// <exception cref="FormatException">The input string is not in the correct format.</exception>
    public static PluginVersion Parse(string input)
    {
        var segments = input.Split('.');
        if (segments.Length != 3)
            throw new FormatException("The input string must be in the format 'major.minor.patch'.");

        var major = int.Parse(segments[0]);
        var minor = int.Parse(segments[1]);
        var patch = int.Parse(segments[2]);
        
        if(major < 0 || minor < 0 || patch < 0)
            throw new FormatException("The major, minor, and patch numbers must be greater than or equal to 0.");
        
        return new PluginVersion(major, minor, patch);
    }
    
    /// <summary>
    /// Converts the plugin version number to a string in the format 'major.minor.patch'.
    /// </summary>
    /// <returns>The plugin version number as a string.</returns>
    public override string ToString() => $"{this.Major}.{this.Minor}.{this.Patch}";
    
    /// <summary>
    /// Compares the plugin version number to another plugin version number.
    /// </summary>
    /// <param name="other">The other plugin version number to compare to.</param>
    /// <returns>A value indicating the relative order of the plugin version numbers.</returns>
    public int CompareTo(PluginVersion other)
    {
        var majorCompare = this.Major.CompareTo(other.Major);
        if (majorCompare != 0)
            return majorCompare;
        
        var minorCompare = this.Minor.CompareTo(other.Minor);
        if (minorCompare != 0)
            return minorCompare;
        
        return this.Patch.CompareTo(other.Patch);
    }
    
    public static bool operator >(PluginVersion left, PluginVersion right) => left.CompareTo(right) > 0;
    
    public static bool operator <(PluginVersion left, PluginVersion right) => left.CompareTo(right) < 0;
    
    public static bool operator >=(PluginVersion left, PluginVersion right) => left.CompareTo(right) >= 0;
    
    public static bool operator <=(PluginVersion left, PluginVersion right) => left.CompareTo(right) <= 0;
}