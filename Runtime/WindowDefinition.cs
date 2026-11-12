using UnityEngine;

namespace EasyUI
{
    [CreateAssetMenu(menuName = "EasyUI/Window Definition")]
    public sealed class WindowDefinition : ScriptableObject
    {
        public UIWindow Prefab;
        public WindowPolicy Policy = new WindowPolicy();
    }
}
