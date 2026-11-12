using System;
using System.Collections.Generic;
using System.Text;
using EasyBuilding;
using EasyBuilding.Integrations;
using EasyLocalization;
using EasyResources;
using EasyUI.Integrations.Building;
using UnityEngine;

namespace EasyUI.Integrations
{
    /// <summary>Authored base costs and current actor-account availability; placement/economy still determine the final charge.</summary>
    [AddComponentMenu("EasyUI/Integrations/Building Catalog Presentation")]
    public sealed class BuildingCatalogPresentation : MonoBehaviour, IBuildingCatalogPresentation
    {
        public LocalizedCatalogCache Cache;
        public BuildingResources Resources;
        public event Action Changed;
        private LocalizedCatalogCache subscribedCache;
        private BuildingSession session;
        private readonly List<ResourceStore> stores = new List<ResourceStore>();
        private readonly Dictionary<string, ResourceDefinition> definitions = new Dictionary<string, ResourceDefinition>(StringComparer.Ordinal);
        private readonly StringBuilder buffer = new StringBuilder();
        private void OnEnable() => RefreshSources();
        private void OnDisable() => Unsubscribe();
        public void Bind(BuildingSession value) { session = value; RefreshSources(); }
        public void Bind(BuildingResources resources, LocalizedCatalogCache cache)
        { Resources = resources; Cache = cache; RefreshSources(); }
        public void RefreshSources()
        {
            Unsubscribe(); definitions.Clear();
            if (!isActiveAndEnabled) return;
            subscribedCache = Cache; if (subscribedCache) subscribedCache.Changed += Refresh;
            if (Resources)
            {
                foreach (var resource in Resources.Resources) if (resource) definitions[resource.Id] = resource;
                foreach (var alias in Resources.Aliases) if (alias.Resource && !string.IsNullOrEmpty(alias.Id)) definitions[alias.Id] = alias.Resource;
                if (session != null && !session.IsDisposed)
                {
                    var accounts = Resources.AccountsProvider as IBuildingResourceAccounts ?? Resources;
                    accounts.GetStores(session.Actor, stores, out _);
                    for (int i = stores.Count - 1; i >= 0; i--)
                        if (stores[i] == null || stores.IndexOf(stores[i]) != i) stores.RemoveAt(i);
                    foreach (var store in stores) store.Changed += Refresh;
                }
            }
            Refresh();
        }
        private void Unsubscribe()
        { if (subscribedCache) subscribedCache.Changed -= Refresh; subscribedCache = null; foreach (var store in stores) store.Changed -= Refresh; stores.Clear(); }
        public void Refresh() => Changed?.Invoke();
        public string Text(string key, string fallback) => Cache ? Cache.Get(key, fallback) : fallback;
        public string Name(BuildingDefinition definition) => definition ? Text("building." + definition.Id, definition.Name) : "";
        public string Details(BuildingDefinition definition)
        {
            if (!definition) return "";
            if (definition.Payment == BuildingPayment.Free) return Text("ui.building.free", "Free");
            buffer.Clear(); buffer.Append(Text("ui.building.baseCosts", "Base (available / cost): "));
            bool first = true;
            foreach (var cost in definition.Costs)
            {
                if (!first) buffer.Append(" · "); first = false;
                if (definitions.TryGetValue(cost.ResourceId, out var resource))
                {
                    long available = 0;
                    foreach (var store in stores) { long amount = store.GetAvailable(resource); available = amount > long.MaxValue - available ? long.MaxValue : available + amount; }
                    buffer.Append(Text("resource." + resource.Id, resource.DisplayName)).Append(": ")
                        .Append(resource.Format(available)).Append(" / ").Append(resource.Format(cost.Amount));
                }
                else buffer.Append(cost.ResourceId).Append(": ? / ").Append(cost.Amount);
            }
            if (first) buffer.Append('0');
            return buffer.ToString();
        }
    }
}
