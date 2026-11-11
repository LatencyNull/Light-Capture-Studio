using System.IO;
using System.Text.Json;

namespace LightCapture.Core.Services;

/// <summary>
/// Gestiona el idioma de destino para la traducción instantánea.
/// Restringido estrictamente a Español ('es') e Inglés ('en') por defecto.
/// </summary>
public static class TranslationLanguageManager
{
    public const string Spanish = "es";
    public const string English = "en";

    private static readonly string SettingsFile = AppPaths.GetRoamingSettingsFile("translation_settings.json");

    private static string _currentLanguage = Spanish;
    private static readonly object _lock = new();

    public static string CurrentLanguage
    {
        get
        {
            lock (_lock)
            {
                return _currentLanguage;
            }
        }
        set => SetLanguage(value);
    }

    public static string CurrentLanguageDisplayName =>
        CurrentLanguage == English ? "English" : "Spanish";

    public static string CurrentLanguageShortName =>
        CurrentLanguage == English ? "English" : "Spanish";

    public static string CurrentLanguageBadge =>
        CurrentLanguage == English ? "EN" : "ES";

    public static string OtherLanguageCode =>
        CurrentLanguage == English ? Spanish : English;

    public static string OtherLanguageDisplayName =>
        CurrentLanguage == English ? "Spanish" : "English";

    public static event Action<string>? LanguageChanged;

    static TranslationLanguageManager()
    {
        LoadPreference();
    }

    public static bool SetLanguage(string? lang)
    {
        string normalized = NormalizeLanguage(lang);
        lock (_lock)
        {
            if (_currentLanguage == normalized) return false;
            _currentLanguage = normalized;
        }

        SavePreference();
        LanguageChanged?.Invoke(normalized);
        return true;
    }

    public static string ToggleLanguage()
    {
        string next = CurrentLanguage == English ? Spanish : English;
        SetLanguage(next);
        return CurrentLanguage;
    }

    public static string NormalizeLanguage(string? lang)
    {
        if (string.IsNullOrWhiteSpace(lang)) return Spanish;
        string l = lang.Trim().ToLowerInvariant();
        if (l == "en" || l == "english" || l == "inglés" || l == "ingles")
        {
            return English;
        }
        return Spanish; // Solo español e inglés permitidos por defecto
    }

    public static void LoadPreference()
    {
        try
        {
            if (File.Exists(SettingsFile))
            {
                string json = File.ReadAllText(SettingsFile);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("target_language", out var prop))
                {
                    lock (_lock)
                    {
                        _currentLanguage = NormalizeLanguage(prop.GetString());
                    }
                }
            }
        }
        catch
        {
            lock (_lock)
            {
                _currentLanguage = Spanish;
            }
        }
    }

    public static void SavePreference()
    {
        try
        {
            string? dir = Path.GetDirectoryName(SettingsFile);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string current;
            lock (_lock)
            {
                current = _currentLanguage;
            }

            var data = new { target_language = current };
            string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFile, json);
        }
        catch
        {
            // Silently ignore IO issues during settings saving
        }
    }
}
