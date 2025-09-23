using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace AIStudio.Components;

public abstract class ConfigurationBaseCore : ConfigurationBase
{
    private protected sealed override RenderFragment Body => this.BuildRenderTree;

    // Allow content to be provided by a .razor file but without 
    // overriding the content of the base class
    protected new virtual void BuildRenderTree(RenderTreeBuilder builder)
    {
    }
}