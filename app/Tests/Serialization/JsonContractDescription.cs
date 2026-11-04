using System.Text;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace AIStudio.Tests.Serialization;

/// <summary>
/// Describes the contract System.Text.Json uses for a type as text, so that two contracts can be compared and
/// a difference shows up readable in the message of a test.
/// </summary>
public static class JsonContractDescription
{
    /// <summary>
    /// Describes everything in a contract which decides what gets written and what a read accepts.
    /// </summary>
    /// <param name="typeInfo">The contract.</param>
    /// <returns>One line for the type, and one line per property, polymorphism, and callback.</returns>
    public static string Describe(JsonTypeInfo typeInfo)
    {
        //
        // For a struct, reflection takes the default of the struct and sets every property, while source generation
        // passes the values to the constructor of a positional record struct. Both give the same struct, as long as
        // no parameter has a default value of its own, which JsonDefaultsTests checks. A class with required
        // properties is alike: source generation sets them while it creates the object, reflection right after.
        // Hence, the description leaves out how such a type gets created:
        //
        var isCreatedWithItsValues = typeInfo.Kind is JsonTypeInfoKind.Object && (typeInfo.Type.IsValueType || typeInfo.Properties.Any(property => property.IsRequired));
        var creation = isCreatedWithItsValues
            ? "created with its values"
            : $"converter {ConverterName(typeInfo.Converter)}, created {(typeInfo.CreateObject is null ? "otherwise" : "without arguments")}";

        var text = new StringBuilder();
        text.AppendLine($"{TypeName(typeInfo.Type)}: {typeInfo.Kind}, {creation}");
        text.AppendLine($"  handling: numbers {typeInfo.NumberHandling}, unmapped members {typeInfo.UnmappedMemberHandling}, object creation {typeInfo.PreferredPropertyObjectCreationHandling}");
        text.AppendLine($"  callbacks: serializing {typeInfo.OnSerializing is not null}, serialized {typeInfo.OnSerialized is not null}, deserializing {typeInfo.OnDeserializing is not null}, deserialized {typeInfo.OnDeserialized is not null}");
        if (typeInfo.KeyType is { } keyType)
            text.AppendLine($"  key: {TypeName(keyType)}");

        if (typeInfo.ElementType is { } elementType)
            text.AppendLine($"  element: {TypeName(elementType)}");

        if (typeInfo.PolymorphismOptions is { } polymorphism)
        {
            text.AppendLine($"  polymorphism: discriminator {polymorphism.TypeDiscriminatorPropertyName}, unrecognized ignored {polymorphism.IgnoreUnrecognizedTypeDiscriminators}, unknown types {polymorphism.UnknownDerivedTypeHandling}");
            foreach (var derivedType in polymorphism.DerivedTypes)
                text.AppendLine($"    derived: {TypeName(derivedType.DerivedType)} as {derivedType.TypeDiscriminator}");
        }

        foreach (var property in typeInfo.Properties)
        {
            //
            // An ignored property keeps its name in both contracts, but only reflection keeps its type:
            //
            if (IsIgnored(property))
            {
                text.AppendLine($"  property {property.Name}: ignored");
                continue;
            }

            var setting = isCreatedWithItsValues
                ?$"set {property.Set is not null || property.AssociatedParameter is not null}"
                : $"set {property.Set is not null}, constructor parameter {property.AssociatedParameter?.Name ?? "none"}";

            text.AppendLine($"  property {property.Name}: {TypeName(property.PropertyType)}, order {property.Order}, get {property.Get is not null}, {setting}, required {property.IsRequired}, extension data {property.IsExtensionData}, conditional {property.ShouldSerialize is not null}, converter {ConverterName(property.CustomConverter)}, nullable get {property.IsGetNullable}, nullable set {property.IsSetNullable}, numbers {property.NumberHandling}, object creation {property.ObjectCreationHandling}");
        }

        return text.ToString();
    }

    /// <summary>
    /// Whether System.Text.Json neither writes nor reads a property, e.g., one marked with JsonIgnore.
    /// </summary>
    /// <param name="property">The property of a contract.</param>
    public static bool IsIgnored(JsonPropertyInfo property) => property is { Get: null, Set: null, AssociatedParameter: null };

    /// <summary>
    /// Returns the name of a type the way C# writes it, e.g., Dictionary&lt;String, Int32&gt;.
    /// </summary>
    /// <param name="type">The type.</param>
    public static string TypeName(Type type)
    {
        var arity = type.Name.IndexOf('`');
        var name = arity < 0 ? type.Name : $"{type.Name[..arity]}<{string.Join(", ", type.GetGenericArguments().Select(TypeName))}>";
        return type is { IsNested: true, IsGenericParameter: false } ?$"{TypeName(type.DeclaringType!)}.{name}" : name;
    }

    //
    // Reflection reads the constructor parameters of a record through converters of its own: one for up to four
    // parameters, and others whose names end with "WithReflection". Source generation reads them all with the
    // converter for many parameters, which reads the same JSON. The same goes for immutable collections:
    //
    private static string ConverterName(JsonConverter? converter)
    {
        if (converter is null)
            return "none";

        var name = converter.GetType().Name;
        if (name.Contains("ObjectWithParameterizedConstructorConverter", StringComparison.Ordinal))
            return $"ObjectWithParameterizedConstructorConverter<{TypeName(converter.Type!)}>";

        return TypeName(converter.GetType()).Replace("WithReflection", string.Empty, StringComparison.Ordinal);
    }
}