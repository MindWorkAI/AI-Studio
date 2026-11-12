using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tests.Tools.PluginSystem;

/// <summary>
/// Checks that every type of configuration object has its descriptor.
/// </summary>
/// <remarks>
/// Reading the objects from a configuration plugin and removing the left-over ones go through the
/// descriptors. A type without one would be read by neither: a configuration plugin could define such
/// objects without effect, and the ones a removed plugin left behind would stay in the settings.
/// </remarks>
[TestFixture]
public sealed class PluginConfigurationObjectDescriptorsTests
{
    [Test]
    public void EveryTypeHasExactlyOneDescriptor()
    {
        var describedTypes = PluginConfigurationObjectDescriptors.ALL.Select(descriptor => descriptor.Type).ToList();
        var configurableTypes = Enum.GetValues<PluginConfigurationObjectType>().Where(type => type is not PluginConfigurationObjectType.NONE and not PluginConfigurationObjectType.UNKNOWN);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(describedTypes, Is.EquivalentTo(configurableTypes));
            Assert.That(describedTypes, Is.Unique, "A type described twice would be read and cleaned up twice.");
        }
    }

    [Test]
    public void EveryDescriptorReadsATableOfItsOwn()
    {
        var tableNames = PluginConfigurationObjectDescriptors.ALL.Select(descriptor => descriptor.LuaTableName).ToList();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(tableNames, Has.None.Empty);
            Assert.That(tableNames, Is.Unique, "Two types reading the same table would each take the objects of the other.");
        }
    }

    [Test]
    public void EveryDescriptorIsFoundByItsTypeAndByItsTable()
    {
        using (Assert.EnterMultipleScope())
        {
            foreach (var descriptor in PluginConfigurationObjectDescriptors.ALL)
            {
                Assert.That(PluginConfigurationObjectDescriptors.Get(descriptor.Type), Is.SameAs(descriptor), descriptor.Type.ToString());
                Assert.That(PluginConfigurationObjectDescriptors.TryGet(descriptor.LuaTableName, out var found), Is.True, descriptor.LuaTableName);
                Assert.That(found, Is.SameAs(descriptor), descriptor.LuaTableName);
            }
        }
    }

    [TestCase(PluginConfigurationObjectType.NONE)]
    [TestCase(PluginConfigurationObjectType.UNKNOWN)]
    public void NoneAndUnknownHaveNoDescriptor(PluginConfigurationObjectType type) => Assert.That(() => PluginConfigurationObjectDescriptors.Get(type), Throws.TypeOf<ArgumentOutOfRangeException>());

    [Test]
    public void AnUnknownTableHasNoDescriptor() => Assert.That(PluginConfigurationObjectDescriptors.TryGet("SETTINGS", out _), Is.False);
}