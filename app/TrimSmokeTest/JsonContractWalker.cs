using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace AIStudio.TrimSmokeTest;

/// <summary>
/// Builds the contract System.Text.Json uses for a root and for everything it holds.
/// </summary>
/// <remarks>
/// A type whose contract needs code the trimmer removed fails right here, before any value is read or
/// written, just like it fails in AI Studio. A chat which lacks the property in question fails all the same.
/// </remarks>
public static class JsonContractWalker
{
    /// <summary>
    /// Returns a description of every contract which cannot be built, starting at the given root.
    /// </summary>
    /// <param name="root">The type to start with, and the options AI Studio uses for it.</param>
    /// <returns>One line per failure, naming the path from the root to the type which failed.</returns>
    public static IReadOnlyList<string> FindBrokenContracts(JsonRoot root)
    {
        //
        // The same System.Text.Json does with the first call of Serialize or Deserialize:
        //
        root.Options.MakeReadOnly(populateMissingResolver: true);

        var failures = new List<string>();
        Visit(root.Type, root.Type.Name, root.Options, [], failures);
        return failures;
    }

    private static void Visit(Type type, string path, JsonSerializerOptions options, HashSet<Type> visited, List<string> failures)
    {
        if (!visited.Add(type))
            return;

        JsonTypeInfo typeInfo;
        try
        {
            typeInfo = options.GetTypeInfo(type);
        }
        catch (Exception exception) when (exception is NotSupportedException or InvalidOperationException)
        {
            failures.Add($"{path}: {exception.Message}");
            return;
        }

        foreach (var property in typeInfo.Properties)
            Visit(property.PropertyType, $"{path} → {property.Name}", options, visited, failures);

        if (typeInfo.KeyType is { } keyType)
            Visit(keyType, $"{path} → key", options, visited, failures);

        if (typeInfo.ElementType is { } elementType)
            Visit(elementType, $"{path} → element", options, visited, failures);

        if (typeInfo.PolymorphismOptions is { } polymorphism)
            foreach (var derivedType in polymorphism.DerivedTypes)
                Visit(derivedType.DerivedType, $"{path} → {derivedType.DerivedType.Name}", options, visited, failures);
    }
}