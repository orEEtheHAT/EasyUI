using System;
using System.Threading;
using EasyLocalization;
using EasyUI.Integrations.Input;
using EasyUI.Integrations.Feedbacks;
using UnityEngine;
using UnityEngine.UI;
using LocalizationApi = EasyLocalization.Localization;

namespace EasyUI.Integrations
{
    [RequireComponent(typeof(InputSettingsWindow)), AddComponentMenu("EasyUI/Integrations/Integrated Settings Window")]
    public sealed class IntegratedSettingsWindow : MonoBehaviour, IInputSettingsTextProvider
    {
        public LocalizedCatalogCache Cache;
        public WindowDefinition AudioWindow;
        public event Action Changed;
        private LocalizedCatalogCache subscribed;
        private InputSettingsWindow controls;
        private Button language, audioButton;
        private RectTransform extension;
        private CancellationTokenSource lifetime;
        private bool loading;
        private string failure;
        private UIWindow audioInstance;
        private WindowDefinition preparedAudioDefinition;
        private bool inheritAudioContext, inheritAudioOpen;
        public string GetText(string key, string fallback) => Cache ? Cache.Get("ui.settings." + key, fallback) : fallback;
        private void OnEnable()
        {
            lifetime = new CancellationTokenSource(); controls = GetComponent<InputSettingsWindow>();
            LocalizationApi.Changed += Refresh;
            controls.ViewBuilt += Build; controls.TextProvider = this;
            Bind(Cache); if (controls.ExtrasRoot) Build(controls.ExtrasRoot);
            Initialize(lifetime.Token);
        }
        private void OnDisable()
        {
            lifetime?.Cancel(); lifetime?.Dispose(); lifetime = null;
            LocalizationApi.Changed -= Refresh;
            if (controls) controls.ViewBuilt -= Build;
            if (subscribed) subscribed.Changed -= Refresh;
            subscribed = null; loading = false;
            DisposeAudio();
        }
        private void OnDestroy() => DisposeAudio();
        public void Bind(LocalizedCatalogCache cache)
        {
            if (subscribed) subscribed.Changed -= Refresh;
            Cache = cache; subscribed = isActiveAndEnabled ? Cache : null;
            if (subscribed) subscribed.Changed += Refresh;
            Refresh();
        }
        private async void Initialize(CancellationToken token)
        {
            try { await LocalizationApi.InitializeAsync(); if (!token.IsCancellationRequested && this) Refresh(); }
            catch (OperationCanceledException) { }
            catch (Exception e) { if (!token.IsCancellationRequested && this) { failure = e.Message; Refresh(); } }
        }
        private void Build(RectTransform root)
        {
            if (extension == root && language) return;
            extension = root;
            language = SettingsView.Button(root, "Language", new Vector2(-112, 0), new Vector2(380, 30), NextLanguage);
            audioButton = SettingsView.Button(root, "Audio", new Vector2(196, 0), new Vector2(210, 30), OpenAudio);
            Refresh(); GetComponent<UIWindow>().RefreshNavigation();
        }
        public void Refresh()
        {
            if (language)
            {
                string code = LocalizationApi.LanguageCode;
                string name = code;
                foreach (var locale in LocalizationApi.Languages) if (locale.Identifier.Code == code) { name = locale.LocaleName; break; }
                language.GetComponentInChildren<Text>().text = failure ?? (GetText("language", "Language") + ": " + (Cache ? Cache.Get("language." + code, name) : name));
                language.interactable = !loading && LocalizationApi.Languages.Count > 1;
            }
            if (audioButton) { audioButton.GetComponentInChildren<Text>().text = GetText("audio.title", "Audio & vibration"); audioButton.interactable = AudioWindow; }
            Changed?.Invoke();
        }
        public async void NextLanguage()
        {
            if (loading || lifetime == null) return;
            var languages = LocalizationApi.Languages; if (languages.Count < 2) return;
            int current = -1; for (int i = 0; i < languages.Count; i++) if (languages[i].Identifier.Code == LocalizationApi.LanguageCode) current = i;
            var token = lifetime.Token; loading = true; failure = null; Refresh();
            try { await LocalizationApi.SetLanguageAsync(languages[(current + 1) % languages.Count].Identifier.Code, token); }
            catch (OperationCanceledException) { }
            catch (Exception e) { if (!token.IsCancellationRequested && this) failure = e.Message; }
            finally { if (!token.IsCancellationRequested && this) { loading = false; Refresh(); } }
        }
        public void OpenAudio()
        {
            var window = GetComponent<UIWindow>(); if (!AudioWindow || window.Handle?.IsOpen != true) return;
            if (!audioInstance || preparedAudioDefinition != AudioWindow)
            {
                DisposeAudio();
                if (!AudioWindow.Prefab || !AudioWindow.Prefab.GetComponent<FeedbackSettingsWindow>())
                    throw new InvalidOperationException("Assign an Audio Window definition whose prefab has Feedback Settings Window.");
                audioInstance = IntegrationWindowSet.InstantiateWindow(AudioWindow, transform);
                preparedAudioDefinition = AudioWindow;
                var feedback = audioInstance.GetComponent<WindowFeedbacks>();
                if (feedback) { inheritAudioContext = !feedback.ContextProvider; inheritAudioOpen = !feedback.Open; }
            }
            // Wiring happens while the child is inactive, before OnEnable/Opened and before it acquires input.
            audioInstance.GetComponent<FeedbackSettingsWindow>().TextProvider = this;
            var childFeedback = audioInstance.GetComponent<WindowFeedbacks>();
            var parentFeedback = GetComponent<WindowFeedbacks>();
            if (childFeedback)
            {
                if (inheritAudioContext) childFeedback.ContextProvider = parentFeedback ? parentFeedback.ContextProvider : null;
                if (inheritAudioOpen) childFeedback.Open = parentFeedback ? parentFeedback.Open : null;
            }
            window.Handle.Manager.Open(audioInstance, window.Handle);
        }
        private void DisposeAudio()
        {
            var child = audioInstance; audioInstance = null; preparedAudioDefinition = null;
            if (!child) return;
            var handle = child.Handle;
            if (handle?.Manager) handle.Manager.Close(handle, true);
            if (Application.isPlaying) Destroy(child.gameObject); else DestroyImmediate(child.gameObject);
        }
    }
}
