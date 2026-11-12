using System;
using EasyLocalization;
using EasyResources;
using EasyUI.Integrations.Resources;
using UnityEngine;

namespace EasyUI.Integrations
{
    [AddComponentMenu("EasyUI/Integrations/Localized Resource Label Provider")]
    public sealed class LocalizedResourceLabelProvider : MonoBehaviour, IResourceLabelProvider
    {
        public LocalizedCatalogCache Cache;
        private LocalizedCatalogCache subscribed;
        public event Action Changed;
        private void OnEnable() => Bind(Cache);
        private void OnDisable() { if (subscribed) subscribed.Changed -= Refresh; subscribed = null; }
        public void Bind(LocalizedCatalogCache cache)
        { if (subscribed) subscribed.Changed -= Refresh; Cache = cache; subscribed = isActiveAndEnabled ? Cache : null; if (subscribed) subscribed.Changed += Refresh; Refresh(); }
        public void Refresh() => Changed?.Invoke();
        public string Format(ResourceDefinition resource, long available)
        {
            if (!resource) return "";
            string name = Cache ? Cache.Get("resource." + resource.Id, resource.DisplayName) : resource.DisplayName;
            string format = Cache ? Cache.Get("ui.resource.balance", "{0}  {1}") : "{0}  {1}";
            try { return string.Format(format, name, resource.Format(available)); }
            catch (FormatException) { return name + "  " + resource.Format(available); }
        }
    }
}
