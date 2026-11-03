using System.Text.Json.Serialization;

namespace AIStudio.Tools.DataBackup;

/// <summary>
/// The contracts with which System.Text.Json reads and writes the manifest of a backup and the plan and report of
/// a restore, built at compile time.
/// </summary>
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(DataBackupManifest))]
[JsonSerializable(typeof(DataRestorePlan))]
[JsonSerializable(typeof(DataRestoreReport))]
internal sealed partial class DataBackupJsonContext : JsonSerializerContext;