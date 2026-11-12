using System;
using EasyResources;

namespace EasyUI.Integrations.Resources
{
    public interface IResourceLabelProvider
    {
        event Action Changed;
        string Format(ResourceDefinition resource, long available);
    }
}
