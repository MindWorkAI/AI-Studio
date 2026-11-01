using System.Text.Json;

namespace AIStudio.Tests.Serialization;

/// <summary>
/// Collects every type System.Text.Json meets while it reads or writes a root.
/// </summary>
public static class JsonTypeGraph
{
    /// <summary>
    /// Returns the root and every type it holds: the types of its properties, the keys and elements of its
    /// collections, and its derived types, each of them once and in the order they were met. The type of an
    /// ignored property doesn't count, since System.Text.Json never meets it.
    /// </summary>
    /// <param name="root">The type to start with.</param>
    /// <param name="options">The options whose contracts tell what a type holds.</param>
    public static IReadOnlyList<Type> Collect(Type root, JsonSerializerOptions options)
    {
        var types = new List<Type>();
        Visit(root, options, [], types);
        return types;
    }

    private static void Visit(Type type, JsonSerializerOptions options, HashSet<Type> visited, List<Type> types)
    {
        if (!visited.Add(type))
            return;

        types.Add(type);
        var typeInfo = options.GetTypeInfo(type);
        foreach (var property in typeInfo.Properties.Where(property => !JsonContractDescription.IsIgnored(property)))
            Visit(property.PropertyType, options, visited, types);

        if (typeInfo.KeyType is { } keyType)
            Visit(keyType, options, visited, types);

        if (typeInfo.ElementType is { } elementType)
            Visit(elementType, options, visited, types);

        if (typeInfo.PolymorphismOptions is { } polymorphism)
            foreach (var derivedType in polymorphism.DerivedTypes)
                Visit(derivedType.DerivedType, options, visited, types);
    }
}