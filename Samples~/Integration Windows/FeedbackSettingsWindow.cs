using EasyFeedbacks;
using EasyUI.Integrations.Input;
using UnityEngine;
using UnityEngine.UI;

namespace EasyUI.Integrations
{
    [RequireComponent(typeof(UIWindow)), AddComponentMenu("EasyUI/Integrations/Feedback Settings Window")]
    public sealed class FeedbackSettingsWindow : MonoBehaviour
    {
        private MonoBehaviour textProvider;
        public MonoBehaviour TextProvider { get => textProvider; set { textProvider = value; BindText(); Refresh(); } }
        public FeedbackSettingsState CapturedSettings { get; private set; }
        private IInputSettingsTextProvider text;
        private RectTransform panel;
        private Text title;
        private readonly Text[] labels = new Text[4];
        private readonly Slider[] sliders = new Slider[4];
        private Button haptics, restore, defaults, back;
        private static readonly string[] Keys = { "audio.master", "audio.music", "audio.sfx", "audio.hapticsIntensity" };
        private static readonly string[] Fallback = { "Master volume", "Music", "Sound effects", "Vibration strength" };
        private void OnEnable()
        {
            CapturedSettings = FeedbackSettings.Capture(); FeedbackSettings.Changed += SettingsChanged;
            BindText(); Build(); Refresh();
        }
        private void OnDisable() { FeedbackSettings.Changed -= SettingsChanged; if (text != null) text.Changed -= Refresh; text = null; }
        private void BindText()
        { if (text != null) text.Changed -= Refresh; text = isActiveAndEnabled ? textProvider as IInputSettingsTextProvider : null; if (text != null) text.Changed += Refresh; }
        private string T(string key, string fallback) => text?.GetText(key, fallback) ?? fallback;
        private void SettingsChanged(FeedbackSettingsState _) => Refresh();
        public void Restore() => FeedbackSettings.Restore(CapturedSettings);
        public void SetValue(int index, float value)
        {
            var state = FeedbackSettings.Current;
            switch (index) { case 0: state.MasterVolume = value; break; case 1: state.MusicVolume = value; break; case 2: state.SoundEffectsVolume = value; break; case 3: state.HapticsIntensity = value; break; default: return; }
            FeedbackSettings.Apply(state);
        }
        public void Refresh()
        {
            if (!panel) return;
            var state = FeedbackSettings.Current;
            title.text = T("audio.title", "Audio & vibration");
            float[] values = { state.MasterVolume, state.MusicVolume, state.SoundEffectsVolume, state.HapticsIntensity };
            for (int i = 0; i < 4; i++) { sliders[i].SetValueWithoutNotify(values[i]); labels[i].text = T(Keys[i], Fallback[i]) + "  " + Mathf.RoundToInt(values[i] * 100) + "%"; }
            haptics.GetComponentInChildren<Text>().text = T("audio.haptics", "Vibration") + ": " + (state.HapticsEnabled ? T("on", "On") : T("off", "Off"));
            restore.GetComponentInChildren<Text>().text = T("restore", "Restore"); defaults.GetComponentInChildren<Text>().text = T("defaults", "Defaults"); back.GetComponentInChildren<Text>().text = T("back", "Back");
        }
        private void Build()
        {
            if (panel) return;
            panel = SettingsView.Rect("Audio settings", transform, Vector2.zero, new Vector2(650, 490)); panel.gameObject.AddComponent<Image>().color = new Color(.055f, .08f, .13f);
            title = SettingsView.Label(panel, "Title", new Vector2(0, 195), new Vector2(600, 50), 28);
            for (int i = 0; i < 4; i++)
            {
                int index = i; float y = 125 - i * 55;
                labels[i] = SettingsView.Label(panel, Keys[i], new Vector2(-150, y), new Vector2(285, 45));
                sliders[i] = SettingsView.Slider(panel, Keys[i], y, value => SetValue(index, value));
            }
            haptics = SettingsView.Button(panel, "Vibration", new Vector2(0, -100), new Vector2(360, 40), () => { var s = FeedbackSettings.Current; s.HapticsEnabled = !s.HapticsEnabled; FeedbackSettings.Apply(s); });
            restore = SettingsView.Button(panel, "Restore", new Vector2(-200, -185), new Vector2(180, 40), Restore);
            defaults = SettingsView.Button(panel, "Defaults", new Vector2(0, -185), new Vector2(180, 40), () => FeedbackSettings.Apply(FeedbackSettingsState.Default));
            back = SettingsView.Button(panel, "Back", new Vector2(200, -185), new Vector2(180, 40), () => GetComponent<UIWindow>().Close());
            var window = GetComponent<UIWindow>(); window.FirstSelected = sliders[0]; window.RefreshNavigation();
        }
    }
}
