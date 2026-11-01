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
        var text = new StringBuilder();
        text.AppendLine($"{TypeName(typeInfo.Type)}: {typeInfo.Kind}, converter {ConverterName(typeInfo.Converter)}, created {(typeInfo.CreateObject is null ? "otherwise" : "without arguments")}");
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
            text.AppendLine($"  property {property.Name}: {TypeName(property.PropertyType)}, order {property.Order}, get {property.Get is not null}, set {property.Set is not null}, required {property.IsRequired}, extension data {property.IsExtensionData}, conditional {property.ShouldSerialize is not null}, converter {ConverterName(property.CustomConverter)}, constructor parameter {property.AssociatedParameter?.Name ?? "none"}, nullable get {property.IsGetNullable}, nullable set {property.IsSetNullable}, numbers {property.NumberHandling}, object creation {property.ObjectCreationHandling}");

        return text.ToString();
    }

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
    // Reflection reads the constructor parameters of a record through converters of its own, whose names end
    // with "WithReflection". Source generation does the same with the plain ones, which read the same JSON:
    //
    private static string ConverterName(JsonConverter? converter) => converter is null ? "none" : TypeName(converter.GetType()).Replace("WithReflection", string.Empty, StringComparison.Ordinal);
}