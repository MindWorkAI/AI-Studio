// ReSharper disable ClassNeverInstantiated.Global
namespace AIStudio.Tools.Metadata;

[AttributeUsage(AttributeTargets.Assembly)]
public class MetaDataArchitecture(string architecture) : Attribute
{
    public string Architecture => architecture;
}