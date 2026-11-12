using System;
using EasyBuilding;

namespace EasyUI.Integrations.Building
{
    public interface IBuildingCatalogPresentation
    {
        event Action Changed;
        void Bind(BuildingSession session);
        string Name(BuildingDefinition definition);
        string Details(BuildingDefinition definition);
        string Text(string key, string fallback);
    }
}
