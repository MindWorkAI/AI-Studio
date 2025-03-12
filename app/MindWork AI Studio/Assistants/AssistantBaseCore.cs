using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace AIStudio.Assistants;

//
// See https://stackoverflow.com/a/77300384/2258393 for why this class is necessary
//

public abstract class AssistantBaseCore<TSettings> : AssistantBase<TSettings> where TSettings : IComponent
{
    private protected sealed override RenderFragment Body => this.BuildRenderTree;

    // Allow content to be provided by a .razor file but without 
    // overriding the content of the base class
    protected new virtual void BuildRenderTree(RenderTreeBuilder builder)
    {
    }
}