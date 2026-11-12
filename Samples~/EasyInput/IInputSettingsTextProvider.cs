using System;

namespace EasyUI.Integrations.Input
{
    /// <summary>Supplies labels/templates, leaving the controls window as the sole writer of its Text components.</summary>
    public interface IInputSettingsTextProvider
    {
        event Action Changed;
        string GetText(string key, string fallback);
    }
}
