using AIStudio.Tools.PluginSystem;

using Lua;

namespace AIStudio.Tests.Tools.PluginSystem;

/// <summary>
/// Checks that a plugin can split its code into several Lua files and load them with require.
/// </summary>
/// <remarks>
/// Long plugins keep parts of their code in files of their own: the plugins AI Studio ships keep their
/// icon in icon.lua, and larger plugins arrange their modules in subdirectories. Whatever the sandbox
/// of the plugins blocks, loading these files from the plugin's own directory has to keep working.
/// </remarks>
[TestFixture]
public sealed class PluginModuleLoadingTests
{
    private string testDirectory = string.Empty;
    private string pluginDirectory = string.Empty;

    [SetUp]
    public void CreatePluginDirectory()
    {
        //
        // The module loader only works inside a base directory, which is the plugins directory of the
        // app for an installed plugin. The plugin therefore gets a base directory of its own here:
        //
        this.testDirectory = Path.Combine(Path.GetTempPath(), $"ai-studio-plugin-modules-{Guid.NewGuid():N}");
        this.pluginDirectory = Path.Combine(this.testDirectory, "plugin");
        Directory.CreateDirectory(this.pluginDirectory);
    }

    [TearDown]
    public void RemovePluginDirectory()
    {
        try
        {
            Directory.Delete(this.testDirectory, true);
        }
        catch (IOException)
        {
            // A temporary directory we could not remove says nothing about the code under test.
        }
    }

    [Test]
    public async Task AModuleNextToThePluginLoads()
    {
        this.WriteModule("helpers.lua", "return { answer = 42 }");

        var answer = await this.RunAsync("""return require("helpers").answer""");

        Assert.That(answer.Read<double>(), Is.EqualTo(42));
    }

    [Test]
    public async Task AModuleInASubdirectoryLoads()
    {
        this.WriteModule("lib/helpers.lua", "return { answer = 42 }");

        var answer = await this.RunAsync("""return require("lib/helpers").answer""");

        Assert.That(answer.Read<double>(), Is.EqualTo(42));
    }

    [Test]
    public async Task AModuleTwoSubdirectoriesDownLoads()
    {
        this.WriteModule("lib/deep/more.lua", "return { answer = 42 }");

        var answer = await this.RunAsync("""return require("lib/deep/more").answer""");

        Assert.That(answer.Read<double>(), Is.EqualTo(42));
    }

    [Test]
    public async Task AModuleLoadsAnotherModule()
    {
        this.WriteModule("lib/outer.lua", """
                                          local inner = require("lib/inner")
                                          return { answer = inner.answer + 1 }
                                          """);
        this.WriteModule("lib/inner.lua", "return { answer = 41 }");

        var answer = await this.RunAsync("""return require("lib/outer").answer""");

        Assert.That(answer.Read<double>(), Is.EqualTo(42));
    }

    [Test]
    public async Task TheGlobalsOfAModuleAreVisibleToThePlugin()
    {
        //
        // This is how the shipped plugins use their icon.lua: it returns nothing and sets a global.
        //
        this.WriteModule("icon.lua", """SVG = "<svg/>" """);

        var icon = await this.RunAsync("""
                                       require("icon")
                                       return SVG
                                       """);

        Assert.That(icon.Read<string>(), Is.EqualTo("<svg/>"));
    }

    [TestCaseSource(nameof(ShippedPluginsWithAnIconFile))]
    public async Task AShippedPluginLoadsItsIconFromItsOwnFile(string relativeDirectory)
    {
        //
        // Only the code of the plugin runs here, not the initialization of its type: a configuration
        // plugin, for instance, needs the services of the running app for that. What counts is
        // that the icon arrives from icon.lua in the runtime every plugin gets.
        //
        var directory = Path.Combine(AppContext.BaseDirectory, relativeDirectory);
        using var state = PluginFactory.CreateLuaState(directory, directory);
        await state.DoStringAsync(await File.ReadAllTextAsync(Path.Combine(directory, "plugin.lua")));

        Assert.That(state.Environment["ICON_SVG"].Read<string>(), Is.EqualTo(await ReadIconAsync(directory)), "The plugin did not get its icon from its icon.lua.");
    }

    /// <summary>
    /// The plugins shipped with AI Studio which keep their icon in an icon.lua next to their plugin.lua.
    /// </summary>
    private static IEnumerable<string> ShippedPluginsWithAnIconFile() =>
        Directory.EnumerateFiles(Path.Combine(AppContext.BaseDirectory, "Plugins"), "icon.lua", SearchOption.AllDirectories)
            .Select(iconFile => Path.GetDirectoryName(iconFile)!)
            .Where(directory => File.Exists(Path.Combine(directory, "plugin.lua")))
            .Select(directory => Path.GetRelativePath(AppContext.BaseDirectory, directory))
            .Order(StringComparer.Ordinal);

    /// <summary>
    /// Reads the icon of an icon.lua on its own, without the plugin around it.
    /// </summary>
    private static async Task<string> ReadIconAsync(string directory)
    {
        using var state = LuaState.Create();
        await state.DoStringAsync(await File.ReadAllTextAsync(Path.Combine(directory, "icon.lua")));
        return state.Environment["SVG"].Read<string>();
    }

    private void WriteModule(string relativePath, string code)
    {
        var path = Path.Combine(this.pluginDirectory, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, code);
    }

    /// <summary>
    /// Runs Lua code in the same runtime a plugin from the plugin directory runs in.
    /// </summary>
    private async Task<LuaValue> RunAsync(string code)
    {
        using var state = PluginFactory.CreateLuaState(this.pluginDirectory, this.testDirectory);
        var results = await state.DoStringAsync(code);
        return results[0];
    }
}