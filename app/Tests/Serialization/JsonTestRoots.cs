using AIStudio.Assistants.SWOTAnalysis;
using AIStudio.Chat;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Settings.DataModel.PreviousModels;
using AIStudio.Tools;
using AIStudio.Tools.Databases.ProvenanceStore;
using AIStudio.Tools.DataBackup;

namespace AIStudio.Tests.Serialization;

/// <summary>
/// The types which AI Studio reads or writes as JSON on their own, for the tests which compare the contracts of
/// System.Text.Json.
/// </summary>
/// <remarks>
/// Only the outermost types belong here: whatever they hold gets checked along with them. Add a type whenever
/// AI Studio starts to store a new kind of file, with the options it stores the file with.
/// </remarks>
public static class JsonTestRoots
{
    /// <summary>
    /// The data AI Studio keeps on the disk of the user.
    /// </summary>
    public static IEnumerable<TestCaseData> Stored()
    {
        TestHost.PrepareStaticState();

        // Chats, in the workspaces, among the temporary chats, and in chat archives:
        yield return Case(new(typeof(ChatThread), WorkspaceBehaviour.JSON_OPTIONS));

        // The settings, and their older versions, which the migrations still read:
        yield return Case(new(typeof(Data), SettingsManager.JSON_OPTIONS));
        yield return Case(new(typeof(DataV1V3), SettingsManager.JSON_OPTIONS));
        yield return Case(new(typeof(DataV4), SettingsManager.JSON_OPTIONS));
        yield return Case(new(typeof(DataV5), SettingsManager.JSON_OPTIONS));

        //
        // The values of managed settings, which get written and read on their own, as the type of their
        // setting. That type is often an interface, such as ISet<string>, which the settings themselves
        // never use:
        //
        foreach (var valueType in ManagedSettingValueTypes())
            yield return Case(new(valueType, SettingsManager.JSON_OPTIONS));

        // The SWOT analysis, which a chat stores as the presentation of a message:
        yield return Case(new(typeof(SwotAnalysisResult), SwotAnalysisContentPresentation.JSON_OPTIONS), "read");
        yield return Case(new(typeof(SwotAnalysisResult), SwotAnalysisContentPresentation.WRITE_OPTIONS), "written");

        // Backups and their restore:
        yield return Case(new(typeof(DataBackupManifest), DataBackupFormat.JSON_OPTIONS));
        yield return Case(new(typeof(DataRestorePlan), DataBackupFormat.JSON_OPTIONS));
        yield return Case(new(typeof(DataRestoreReport), DataBackupFormat.JSON_OPTIONS));

        // The text around a passage which the provenance store keeps:
        yield return Case(new(typeof(IReadOnlyList<string>), SqliteProvenanceStoreClientImplementation.JSON_OPTIONS));
        yield return Case(new(typeof(List<string>), SqliteProvenanceStoreClientImplementation.JSON_OPTIONS));
    }

    private static TestCaseData Case(JsonTestRoot root, string? purpose = null)
    {
        //
        // The same System.Text.Json does with the first call of Serialize or Deserialize. Only then, the
        // options hand out contracts:
        //
        root.Options.MakeReadOnly(populateMissingResolver: true);
        return new TestCaseData(root).SetArgDisplayNames(purpose is null ? root.ToString() : $"{root}, {purpose}");
    }

    private static IEnumerable<Type> ManagedSettingValueTypes()
    {
        //
        // Creating the settings registers every managed setting, just like the start of the app does:
        //
        _ = new Data();
        return ManagedConfiguration.RegisteredSettings
            .Select(setting => setting.GetType().GetGenericArguments()[1])
            .Distinct()
            .OrderBy(JsonContractDescription.TypeName, StringComparer.Ordinal);
    }
}