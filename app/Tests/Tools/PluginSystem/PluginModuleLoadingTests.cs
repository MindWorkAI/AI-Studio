using AIStudio.Tools.PluginSystem;

using Lua;
using Lua.Standard;

namespace AIStudio.Tests.Tools.PluginSystem;

/// <summary>
/// Checks that a plugin can split its code into several Lua files and load them with require, and that
/// it reaches no other file.
/// </summary>
/// <remarks>
/// Long plugins keep parts of their code in files of their own: the plugins AI Studio ships keep their
/// icon in icon.lua, and larger plugins arrange their modules in subdirectories. Whatever the sandbox
/// of the plugins blocks, loading these files from the plugin's own directory has to keep working.<br/><br/>
/// Every other file stays out of reach: the Lua runtime would otherwise look for a module it does not
/// find next to the plugin in the working directory of the app, and dofile and loadfile would open any
/// path. Plugins are shared between users, and code written by a model runs in the same runtime.
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

    [Test]
    public void AModuleOutsideThePluginDirectoryIsNotLoaded()
    {
        this.WriteModuleOutsideThePlugin();

        Assert.That(async () => await this.RunAsync("""return require("../other/secret").answer"""), Throws.InstanceOf<LuaRuntimeException>());
    }

    [Test]
    public void AModuleNamedByItsAbsolutePathIsNotLoaded()
    {
        var path = Path.ChangeExtension(this.WriteModuleOutsideThePlugin(), null);

        Assert.That(async () => await this.RunAsync($"return require([[{path}]]).answer"), Throws.InstanceOf<LuaRuntimeException>());
    }

    [Test]
    public void AModuleInTheWorkingDirectoryIsNotLoaded()
    {
        var moduleName = $"ai-studio-module-probe-{Guid.NewGuid():N}";
        var path = Path.Combine(Environment.CurrentDirectory, $"{moduleName}.lua");
        File.WriteAllText(path, "return { answer = 42 }");

        try
        {
            Assert.That(async () => await this.RunAsync($"""return require("{moduleName}").answer"""), Throws.InstanceOf<LuaRuntimeException>(), "A plugin with a directory found a module in the working directory.");
            Assert.That(async () => await RunWithoutPluginDirectoryAsync($"""return require("{moduleName}").answer"""), Throws.InstanceOf<LuaRuntimeException>(), "Code without a directory found a module in the working directory.");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void APluginCannotRedirectTheSearchForModules()
    {
        var directory = Path.GetDirectoryName(this.WriteModuleOutsideThePlugin());

        Assert.That(async () => await this.RunAsync($"""
                                                     package.path = [[{directory}/?.lua]]
                                                     return require("secret").answer
                                                     """), Throws.InstanceOf<LuaRuntimeException>());
    }

    [Test]
    public async Task APluginCannotProbeForFiles()
    {
        var directory = Path.GetDirectoryName(this.WriteModuleOutsideThePlugin());
        var probe = $"""return package.searchpath("secret", [[{directory}/?.lua]], ".", "/")""";

        //
        // A runtime with the default file system finds the file. Without this counter check, a call
        // which fails for another reason would pass this test without proving anything:
        //
        using var defaultState = LuaState.Create();
        defaultState.OpenModuleLibrary();
        var foundByDefault = await defaultState.DoStringAsync(probe);
        Assert.That(foundByDefault[0].Type, Is.EqualTo(LuaValueType.String), "The probe itself is broken.");

        var found = await this.RunAsync(probe);

        Assert.That(found.Type, Is.EqualTo(LuaValueType.Nil), "The plugin learned that a file outside its directory exists.");
    }

    [TestCase("dofile", true)]
    [TestCase("dofile", false)]
    [TestCase("loadfile", true)]
    [TestCase("loadfile", false)]
    public void AFileCannotBeRunByItsPath(string function, bool insideThePluginDirectory)
    {
        //
        // Even inside the plugin's directory, since such a path would be read relative to the working
        // directory of the app, not to the plugin. Plugins load their own files with require instead:
        //
        this.WriteModule("helpers.lua", "return { answer = 42 }");
        var path = insideThePluginDirectory ? Path.Combine(this.pluginDirectory, "helpers.lua") : this.WriteModuleOutsideThePlugin();

        Assert.That(async () => await this.RunAsync($"local result = {function}([[{path}]])"), Throws.InstanceOf<LuaRuntimeException>());
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
    /// Writes a module next to the plugin's directory, inside the same base directory.
    /// </summary>
    /// <returns>The path of the module, with forward slashes, so it can stand in Lua code on every system.</returns>
    private string WriteModuleOutsideThePlugin()
    {
        var path = Path.Combine(this.testDirectory, "other", "secret.lua");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "return { answer = 42 }");
        return path.Replace('\\', '/');
    }

    /// <summary>
    /// Runs Lua code in the same runtime a plugin from the plugin directory runs in.
    /// </summary>
    private async Task<LuaValue> RunAsync(string code)
    {
        using var state = PluginFactory.CreateLuaState(this.pluginDirectory, this.testDirectory);
        var results = await state.DoStringAsync(code);
        return results.Length > 0 ? results[0] : LuaValue.Nil;
    }

    /// <summary>
    /// Runs Lua code in the runtime of a plugin which has no directory, such as one a model just wrote.
    /// </summary>
    private static async Task<LuaValue> RunWithoutPluginDirectoryAsync(string code)
    {
        using var state = PluginFactory.CreateLuaState(null);
        var results = await state.DoStringAsync(code);
        return results.Length > 0 ? results[0] : LuaValue.Nil;
    }
}