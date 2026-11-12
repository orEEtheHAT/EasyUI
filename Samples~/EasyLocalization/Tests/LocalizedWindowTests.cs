using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace EasyUI.Integrations.Localization.Tests
{
    public sealed class LocalizedWindowTests
    {
        private LocalizationSettings previous;
        private EasyLocalization.LocalizationConfig previousConfig;
        private readonly List<Object> created = new List<Object>();
        private GameObject go;
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (go) Object.DestroyImmediate(go);
            EasyLocalization.Localization.Reset(); LocalizationSettings.Instance = previous;
            yield return Wait(EasyLocalization.Localization.ReinitializeAsync(previousConfig));
            for (int i = created.Count - 1; i >= 0; i--) if (created[i]) Object.DestroyImmediate(created[i]);
            created.Clear();
        }
        private sealed class TestLocales : ILocalesProvider
        {
            public List<Locale> Locales { get; } = new List<Locale>();
            public Locale GetLocale(LocaleIdentifier id) => Locales.FirstOrDefault(x => x.Identifier == id);
            public void AddLocale(Locale locale) => Locales.Add(locale);
            public bool RemoveLocale(Locale locale) => Locales.Remove(locale);
        }
        private sealed class Tables : ITableProvider
        {
            internal readonly Dictionary<string, StringTable> Values = new Dictionary<string, StringTable>();
            public AsyncOperationHandle<T> ProvideTableAsync<T>(string name, Locale locale) where T : LocalizationTable =>
                Values.TryGetValue(locale.Identifier.Code, out var table) ? Addressables.ResourceManager.CreateCompletedOperation(table as T, (string)null) : default;
        }
        private static IEnumerator Wait(Task task)
        {
            float deadline = Time.realtimeSinceStartup + 10;
            while (!task.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(task.IsCompleted, "Localization timed out."); if (task.IsFaulted) throw task.Exception.GetBaseException();
        }
        [UnityTest] public IEnumerator LocalizedStringAndPublicLanguageApiRefreshWindowTextAndRespectDisable()
        {
            previous = LocalizationSettings.Instance; previousConfig = EasyLocalization.Localization.Configuration; EasyLocalization.Localization.Reset();
            var settings = ScriptableObject.CreateInstance<LocalizationSettings>(); created.Add(settings);
            var locales = new TestLocales(); var tables = new Tables();
            var shared = ScriptableObject.CreateInstance<SharedTableData>(); created.Add(shared); shared.TableCollectionName = "EasyUI Test"; shared.AddKey("inventory", 7001);
            foreach (var code in new[] { "en", "ru" })
            {
                var locale = Locale.CreateLocale(code); created.Add(locale); locales.AddLocale(locale);
                var table = ScriptableObject.CreateInstance<StringTable>(); created.Add(table); table.SharedData = shared; table.LocaleIdentifier = code;
                table.AddEntry(7001, code == "en" ? "Inventory" : "Инвентарь"); tables.Values.Add(code, table);
            }
            settings.SetAvailableLocales(locales); settings.SetStringDatabase(new LocalizedStringDatabase { TableProvider = tables });
            LocalizationSettings.Instance = settings; LocalizationSettings.StartupLocaleSelectors.Clear(); LocalizationSettings.StartupLocaleSelectors.Add(new SpecificLocaleSelector { LocaleId = "en" });
            var config = ScriptableObject.CreateInstance<EasyLocalization.LocalizationConfig>(); created.Add(config); config.fallbackLanguage = "en";
            {
                yield return Wait(EasyLocalization.Localization.ReinitializeAsync(config));
                go = new GameObject("Localized UI", typeof(RectTransform), typeof(Text)); go.SetActive(false);
                var label = go.GetComponent<Text>(); var adapter = go.AddComponent<LocalizedWindowText>(); adapter.Text = new LocalizedString("EasyUI Test", 7001); go.SetActive(true);
                yield return null; yield return null; Assert.AreEqual("Inventory", label.text);
                yield return Wait(EasyLocalization.Localization.SetLanguageAsync("ru")); yield return null; Assert.AreEqual("Инвентарь", label.text);
                adapter.enabled = false; yield return Wait(EasyLocalization.Localization.SetLanguageAsync("en")); yield return null; Assert.AreEqual("Инвентарь", label.text);
                adapter.enabled = true; yield return null; yield return null; Assert.AreEqual("Inventory", label.text);
            }
        }
    }
}
