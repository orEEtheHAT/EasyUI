using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace EasyUI
{
    public static class InputReleaseGate
    {
        public static bool Held(InputAction action, float threshold = .5f)
        {
            if (action == null) return false;
            var controls = action.controls;
            for (int i = 0; i < controls.Count; i++)
                if (controls[i].EvaluateMagnitude() > threshold) return true;
            return false;
        }

        /// <summary>Used only during gameplay handoff. Prevents a release/confirm frame reaching gameplay.</summary>
        public static bool AnyButtonHeld()
        {
            foreach (var device in InputSystem.devices)
            {
                var controls = device.allControls;
                for (int i = 0; i < controls.Count; i++)
                    if (controls[i] is ButtonControl button && button.isPressed) return true;
            }
            return false;
        }
    }
}
