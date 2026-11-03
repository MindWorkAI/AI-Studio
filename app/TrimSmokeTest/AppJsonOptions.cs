using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIStudio.TrimSmokeTest;

/// <summary>
/// Finds the options with which AI Studio reads and writes JSON.
/// </summary>
/// <remarks>
/// AI Studio keeps each kind of options in a static field or property, next to the code which uses them, so
/// these are the options which reach System.Text.Json. A list written by hand would miss the next new one.
/// The tests of the app compile this file, too, see Tests.csproj, so that both check the same options.
/// </remarks>
public static class AppJsonOptions
{
    private const BindingFlags STATIC_MEMBERS = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    /// <summary>
    /// Returns the options of every static field and property of AI Studio which holds some.
    /// </summary>
    /// <remarks>
    /// Reading a member runs the type initializer of its type. Hence, the static state which the start of
    /// the app fills has to be there before, Program.LOGGER_FACTORY above all.
    /// </remarks>
    /// <returns>The options by the name of their type and member, e.g., SettingsManager.JSON_OPTIONS.</returns>
    public static IReadOnlyDictionary<string, JsonSerializerOptions> FindAll()
    {
        var found = new SortedDictionary<string, JsonSerializerOptions>(StringComparer.Ordinal);

        //
        // The Program of AI Studio, not the one of the trim smoke test, which is closer from here. Only within
        // the tests, which compile this file, too, the qualifier is redundant, and Rider reads it there:
        //
        // ReSharper disable once RedundantNameQualifier
        foreach (var type in typeof(AIStudio.Program).Assembly.GetTypes())
        {
            //
            // A context keeps options of its own for its Default instance, which never reach System.Text.Json
            // directly. The static members of an open generic type hold no value at all:
            //
            if (type.ContainsGenericParameters || type.IsSubclassOf(typeof(JsonSerializerContext)))
                continue;

            //
            // The field behind a static auto-property is found as the property already:
            //
            foreach (var field in type.GetFields(STATIC_MEMBERS))
                if (field.FieldType == typeof(JsonSerializerOptions) && !field.IsDefined(typeof(CompilerGeneratedAttribute)) && field.GetValue(null) is JsonSerializerOptions options)
                    found.Add($"{type.Name}.{field.Name}", options);

            foreach (var property in type.GetProperties(STATIC_MEMBERS))
                if (property.PropertyType == typeof(JsonSerializerOptions) && property.GetMethod is not null && property.GetValue(null) is JsonSerializerOptions options)
                    found.Add($"{type.Name}.{property.Name}", options);
        }

        return found;
    }
}