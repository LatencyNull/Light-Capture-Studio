using System.IO;
using System.Text.Json;

namespace LightCapture.Core.Services;

public record HotkeyConfig(
    uint VirtualKey,
    uint Modifiers,
    string DisplayName
);

/// <summary>
/// Gestiona la configuración y persistencia de la tecla global de captura de pantalla.
/// Permite al usuario remapear F9 por cualquier otra tecla o combinación personalizada.
/// </summary>
public static class HotkeySettingsManager
{
    private static readonly string SettingsFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "LightCaptureStudio",
        "hotkey_settings.json"
    );

    // Predeterminado de fábrica: F9 (VK_F9 = 0x78, sin modificadores)
    public const uint DefaultVk = 0x78;
    public const uint DefaultModifiers = 0;
    public const string DefaultDisplayName = "F9";

    private static HotkeyConfig _currentConfig = new(DefaultVk, DefaultModifiers, DefaultDisplayName);
    private static readonly object _lock = new();

    static HotkeySettingsManager()
    {
        LoadSettings();
    }

    public static HotkeyConfig CurrentConfig
    {
        get
        {
            lock (_lock)
            {
                return _currentConfig;
            }
        }
    }

    public static uint CurrentVirtualKey => CurrentConfig.VirtualKey;
    public static uint CurrentModifiers => CurrentConfig.Modifiers;
    public static string CurrentDisplayName => CurrentConfig.DisplayName;

    public static void SaveConfig(uint vk, uint modifiers, string displayName)
    {
        lock (_lock)
        {
            _currentConfig = new HotkeyConfig(vk, modifiers, displayName);
            try
            {
                string dir = Path.GetDirectoryName(SettingsFile)!;
                Directory.CreateDirectory(dir);
                string json = JsonSerializer.Serialize(_currentConfig, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsFile, json);
            }
            catch { }
        }
    }

    private static void LoadSettings()
    {
        lock (_lock)
        {
            try
            {
                if (File.Exists(SettingsFile))
                {
                    string json = File.ReadAllText(SettingsFile);
                    var config = JsonSerializer.Deserialize<HotkeyConfig>(json);
                    if (config != null && config.VirtualKey != 0 && !string.IsNullOrWhiteSpace(config.DisplayName))
                    {
                        _currentConfig = config;
                    }
                }
            }
            catch { }
        }
    }

    public static void ResetToDefault()
    {
        SaveConfig(DefaultVk, DefaultModifiers, DefaultDisplayName);
    }
}
