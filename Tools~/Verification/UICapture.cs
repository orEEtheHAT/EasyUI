using System.IO;
using UnityEngine;

namespace EasyUI.Verification
{
    /// <summary>Offscreen screenshots for the Built-in-renderer verification project.
    /// Hidden Windows players can return an empty backbuffer from ScreenCapture.</summary>
    public static class UICapture
    {
        public static void Save(string path)
        {
            var camera = Camera.main ? Camera.main : Object.FindAnyObjectByType<Camera>();
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            var modes = new RenderMode[canvases.Length]; var cameras = new Camera[canvases.Length]; var distances = new float[canvases.Length];
            var overrides = new bool[canvases.Length]; var orders = new int[canvases.Length];
            var target = new RenderTexture(1280, 720, 24); var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            var originalTarget = camera.targetTexture; var originalActive = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                for (int i = 0; i < canvases.Length; i++)
                {
                    modes[i] = canvases[i].renderMode; cameras[i] = canvases[i].worldCamera; distances[i] = canvases[i].planeDistance;
                    overrides[i] = canvases[i].overrideSorting; orders[i] = canvases[i].sortingOrder;
                }
                for (int i = 0; i < canvases.Length; i++)
                {
                    if (canvases[i].isRootCanvas && modes[i] == RenderMode.ScreenSpaceOverlay)
                    { canvases[i].renderMode = RenderMode.ScreenSpaceCamera; canvases[i].worldCamera = camera; canvases[i].planeDistance = 5; }
                }
                for (int i = 0; i < canvases.Length; i++) { canvases[i].overrideSorting = overrides[i]; canvases[i].sortingOrder = orders[i]; }
                Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                for (int i = 0; i < canvases.Length; i++) if (canvases[i] && canvases[i].isRootCanvas)
                { canvases[i].renderMode = modes[i]; canvases[i].worldCamera = cameras[i]; canvases[i].planeDistance = distances[i]; }
                for (int i = 0; i < canvases.Length; i++) if (canvases[i]) { canvases[i].overrideSorting = overrides[i]; canvases[i].sortingOrder = orders[i]; }
                camera.targetTexture = originalTarget; RenderTexture.active = originalActive;
                Object.Destroy(texture); target.Release(); Object.Destroy(target); Canvas.ForceUpdateCanvases();
            }
        }
    }
}
