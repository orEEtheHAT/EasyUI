using UnityEngine;

namespace EasyUI.Integrations
{
    [CreateAssetMenu(menuName = "EasyUI/Integration Window Set")]
    public sealed class IntegrationWindowSet : ScriptableObject
    {
        public WindowDefinition Interaction, InteractionRadial, Building, BuildingRadial, Settings, Audio;

        /// <summary>Optional once-per-player preparation: wire scene references on an inactive instance, then reuse manager.Open(instance).</summary>
        public static UIWindow InstantiateWindow(WindowDefinition definition, Transform parent = null)
        {
            if (!definition || !definition.Prefab) throw new System.ArgumentException("Assign a definition with a window prefab.", nameof(definition));
            // Generated prefabs are inactive. A temporary inactive parent also makes user-authored active prefabs safe to configure.
            var staging = new GameObject("Prepare UI window"); staging.SetActive(false);
            try
            {
                var instance = Instantiate(definition.Prefab, staging.transform);
                instance.gameObject.SetActive(false); instance.Policy = definition.Policy.Copy(); instance.transform.SetParent(parent, false);
                return instance;
            }
            finally { if (Application.isPlaying) Destroy(staging); else DestroyImmediate(staging); }
        }
    }
}
