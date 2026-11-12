using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;

namespace EasyUI.Integrations.Localization
{
    [RequireComponent(typeof(Text)), AddComponentMenu("EasyUI/Integrations/Localized Window Text")]
    public sealed class LocalizedWindowText : MonoBehaviour
    {
        public LocalizedString Text = new LocalizedString();
        public string Fallback = "";
        private int generation;
        private object[] arguments;
        private CancellationTokenSource pending;
        private void OnEnable() { EasyLocalization.Localization.Changed += Refresh; Text.StringChanged += StringChanged; Refresh(); }
        private void OnDisable()
        {
            EasyLocalization.Localization.Changed -= Refresh; Text.StringChanged -= StringChanged;
            generation++; pending?.Cancel(); pending?.Dispose(); pending = null;
        }
        private void StringChanged(string _) => Refresh();
        public void SetArguments(params object[] values) { arguments = values; Refresh(); }
        public async void Refresh() => await RefreshAsync();
        public async Task RefreshAsync()
        {
            int version = ++generation;
            pending?.Cancel(); pending?.Dispose(); pending = null;
            if (!isActiveAndEnabled) return;
            var request = pending = new CancellationTokenSource();
            try
            {
                string value = await EasyLocalization.Localization.TextAsync(Text, arguments, Fallback, request.Token);
                if (this && isActiveAndEnabled && version == generation) GetComponent<Text>().text = value;
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { if (this && isActiveAndEnabled && version == generation) Debug.LogException(e, this); }
            finally { if (ReferenceEquals(pending, request)) pending = null; request.Dispose(); }
        }
    }
}
