using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIStudio.TrimSmokeTest;

/// <summary>
/// The types which AI Studio reads or writes as JSON, each with the options it uses for them.
/// </summary>
/// <remarks>
/// The options of AI Studio each name the context which knows their types, so the types registered in that
/// context are exactly those which System.Text.Json gets to read or write with these options. Whatever these
/// types hold, i.e., the types of their properties, the elements of their collections, and their derived
/// types, gets checked along with them. A new type, or new options, need nothing here: they are found
/// through their context.
/// </remarks>
public static class JsonRoots
{
    /// <summary>
    /// Returns every type registered in a context of the options of AI Studio, together with these options.
    /// </summary>
    /// <remarks>
    /// Options which several members share are checked once. Like AppJsonOptions.FindAll, this needs the
    /// static state of the app to be filled before.
    /// </remarks>
    public static IReadOnlyList<JsonRoot> FindAll() => AppJsonOptions.FindAll().Values
        .Distinct(ReferenceEqualityComparer.Instance)
        .Cast<JsonSerializerOptions>()
        .SelectMany(options => options.TypeInfoResolverChain
            .OfType<JsonSerializerContext>()
            .SelectMany(RegisteredTypes)
            .Select(type => new JsonRoot(type, options)))
        .ToList();

    private static IEnumerable<Type> RegisteredTypes(JsonSerializerContext context) => context.GetType()
        .GetCustomAttributesData()
        .Where(attribute => attribute.AttributeType == typeof(JsonSerializableAttribute))
        .Select(attribute => (Type)attribute.ConstructorArguments[0].Value!);
}