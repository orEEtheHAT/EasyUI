using EasyFeedbacks;
using UnityEngine;
using UnityEngine.EventSystems;

namespace EasyUI.Integrations.Feedbacks
{
    [AddComponentMenu("EasyUI/Integrations/Window Feedbacks")]
    public sealed class WindowFeedbacks : MonoBehaviour, ISelectHandler, ISubmitHandler, IPointerClickHandler
    {
        public FeedbackPreset Open, Select, Confirm;
        [Tooltip("Optional component implementing IFeedbackContextProvider, e.g. the player's feedback context.")]
        public MonoBehaviour ContextProvider;
        public FeedbackContext PrepareContext()
        {
            var context = FeedbackContext.From(transform);
            return ContextProvider && ContextProvider.isActiveAndEnabled && ContextProvider is IFeedbackContextProvider provider ? provider.Prepare(context) : context;
        }
        private UIWindow window;
        private RadialMenu radial;
        private FeedbackHandle current;
        private int confirmFrame = -1;
        private void OnEnable()
        {
            window = GetComponent<UIWindow>(); radial = GetComponent<RadialMenu>();
            if (window) window.Opened += Opened;
            if (radial) { radial.SelectionChanged += Selected; radial.Confirmed += Confirmed; }
            FeedbackService.Prewarm(Open); FeedbackService.Prewarm(Select); FeedbackService.Prewarm(Confirm);
        }
        private void OnDisable()
        {
            if (window) window.Opened -= Opened;
            if (radial) { radial.SelectionChanged -= Selected; radial.Confirmed -= Confirmed; }
            current.Stop();
        }
        private void Play(FeedbackPreset preset) { if (preset) { current.Stop(); current = FeedbackService.Play(preset, PrepareContext()); } }
        private void Opened() => Play(Open);
        private void Selected(int index) { if (index >= 0) Play(Select); }
        private void Confirmed(RadialItem _) => Confirmation();
        public void OnSelect(BaseEventData _) => Play(Select);
        public void OnSubmit(BaseEventData _) => Confirmation();
        public void OnPointerClick(PointerEventData data) { if (data.button == PointerEventData.InputButton.Left) Confirmation(); }
        private void Confirmation() { if (confirmFrame == Time.frameCount) return; confirmFrame = Time.frameCount; Play(Confirm); }
    }
}
