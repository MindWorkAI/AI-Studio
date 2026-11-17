using System.Text.Json;

using AIStudio.Tools.Rust;
using AIStudio.Tools.Services;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks that the app reads what the runtime tells about itself, the Rust compiler which built it among it.
/// </summary>
/// <remarks>
/// RuntimeInfo in runtime/src/environment.rs names its fields in snake case, and the information page shows
/// the Rust compiler from it. A name which the app spelled differently would leave the page saying "unknown"
/// without failing anywhere. The app asks for this answer while it starts, so an answer without the compiler,
/// as a runtime older than the app sends it, must not fail either. The answers are read with the options of
/// RustService itself.
/// </remarks>
[TestFixture]
public sealed class RuntimeInfoResponseTests
{
    [Test]
    public void TheAppReadsTheRustCompilerWhichBuiltTheRuntime()
    {
        var response = JsonSerializer.Deserialize<RuntimeInfoResponse>("""{"working_directory":"/samples/work","executable_path":"/samples/app/mindwork-ai-studio","linux_package_type":"Flatpak","installation_kind":"User","rust_version":"1.98.1 (commit a1b2c3d4e)"}""", RustService.JSON_OPTIONS);

        Assert.That(response, Is.EqualTo(new RuntimeInfoResponse("/samples/work", "/samples/app/mindwork-ai-studio", LinuxPackageType.FLATPAK, InstallationKind.USER, "1.98.1 (commit a1b2c3d4e)")));
    }

    [Test]
    public void ARuntimeOlderThanTheAppNamesNoRustCompiler()
    {
        var response = JsonSerializer.Deserialize<RuntimeInfoResponse>("""{"working_directory":"/samples/work","executable_path":"/samples/app/mindwork-ai-studio","linux_package_type":"NotApplicable","installation_kind":"Development"}""", RustService.JSON_OPTIONS);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.InstallationKind, Is.EqualTo(InstallationKind.DEVELOPMENT));
            Assert.That(response.RustVersion, Is.Null);
        }
    }
}