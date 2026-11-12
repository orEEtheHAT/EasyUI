using EasyInteraction;
using UnityEngine;

namespace EasyUI.Integrations.Interaction
{
    [AddComponentMenu("EasyUI/Integrations/Open Window Action")]
    public sealed class OpenWindowAction : InteractionAction
    {
        [Tooltip("For a single player, assign directly. Otherwise resolve Window Manager below actor.PlayerRoot.")]
        public WindowManager Manager;
        public WindowDefinition Window;
        public WindowHandle LastWindow { get; private set; }
        private WindowManager Resolve(InteractionActor actor) => Manager ? Manager : actor.PlayerRoot.GetComponentInChildren<WindowManager>(true);
        public override InteractionAvailability GetAvailability(InteractionActor actor) => Window && Resolve(actor)
            ? InteractionAvailability.Available
            : InteractionAvailability.Unavailable(InteractionFailure.InvalidConfiguration, "Assign a Window Definition and a Window Manager.");
        protected override bool OnExecute(InteractionActor actor)
        {
            if (!Resolve(actor).TryOpen(Window, out var handle, out string reason)) { actor.Cancel(this, reason); return true; }
            LastWindow = handle;
            return false;
        }
    }
}
