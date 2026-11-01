using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace AIStudio.Tests.Serialization;

/// <summary>
/// Checks that a property missing in the JSON keeps the value a new object starts with.
/// </summary>
/// <remarks>
/// Files written by an older version of AI Studio lack every property added since. Reading them has to give
/// those properties their defaults, as a new object has them, e.g., a sub-object of the settings instead of
/// null. Source generation in .NET 10 breaks this for init-only properties: it sets them all while creating
/// the object, and a property missing in the JSON gets the default of its type instead (dotnet/runtime#84484).
/// Hence, the types AI Studio stores use set instead of init.<br/><br/>
///
/// A new object is created the way reflection creates it: with its parameterless constructor, or, for a
/// struct, as the default of the struct. Source generation passes the values of a positional record struct
/// to its constructor instead, where a parameter with a default value of its own would differ.
/// </remarks>
[TestFixture]
public sealed class JsonDefaultsTests
{
    [TestCaseSource(typeof(JsonTestRoots), nameof(JsonTestRoots.Stored))]
    public void AnEmptyObjectGetsTheDefaultsOfANewOne(JsonTestRoot root)
    {
        var baseline = JsonReflectionBaseline.Of(root.Options);
        var failures = new List<string>();
        foreach (var type in JsonTypeGraph.Collect(root.Type, root.Options))
        {
            //
            // A class which gets created through its constructor receives every value as an argument, so nothing
            // gets lost there. A required property refuses an empty object anyway:
            //
            var typeInfo = root.Options.GetTypeInfo(type);
            if (typeInfo.Kind is not JsonTypeInfoKind.Object || baseline.GetTypeInfo(type).CreateObject is not { } createObject || typeInfo.Properties.Any(property => property.IsRequired))
                continue;

            var newObject = createObject();
            var otherNewObject = createObject();
            var readObject = JsonSerializer.Deserialize("{}", typeInfo)!;
            foreach (var property in typeInfo.Properties.Where(property => property.Get is not null))
            {
                var expected = ValueAsJson(property, newObject, root.Options);
                var actual = ValueAsJson(property, readObject, root.Options);

                //
                // Some properties start with a value of their own each time, e.g., a new ID. There, the read object
                // just needs any value but the default of the type:
                //
                if (expected == ValueAsJson(property, otherNewObject, root.Options))
                {
                    if (actual != expected)
                        failures.Add($"{JsonContractDescription.TypeName(type)}.{property.Name}: read as {actual}, while a new object has {expected}.");
                }
                else if (actual == DefaultAsJson(property.PropertyType, root.Options))
                    failures.Add($"{JsonContractDescription.TypeName(type)}.{property.Name}: read as {actual}, while a new object gets a value of its own.");
            }
        }

        Assert.That(failures, Is.Empty);
    }

    private static string ValueAsJson(JsonPropertyInfo property, object instance, JsonSerializerOptions options) => JsonSerializer.Serialize(property.Get!(instance), property.PropertyType, options);

    private static string DefaultAsJson(Type type, JsonSerializerOptions options) => type.IsValueType ? JsonSerializer.Serialize(Activator.CreateInstance(type), type, options) : "null";
}