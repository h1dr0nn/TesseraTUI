using System;
using Avalonia.Styling;

namespace Tessera.Agents;

public class SettingsAgent
{
    public event Action<ThemeVariant>? ThemeChanged;

    public ThemeVariant PreferredTheme { get; private set; } = ThemeVariant.Light;

    public void SetTheme(ThemeVariant variant)
    {
        PreferredTheme = variant;
        ThemeChanged?.Invoke(variant);
    }
}
