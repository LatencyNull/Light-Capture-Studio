using System;
using System.IO;
using System.Text.Json;
using WpfApplication = System.Windows.Application;
using MediaColor = System.Windows.Media.Color;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace LightCapture.App.Services;

public enum AppTheme
{
    Dark,
    Light
}

public static class ThemeManager
{
    private static readonly string SettingsFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "LightCaptureStudio",
        "theme.json"
    );

    public static AppTheme CurrentTheme { get; private set; } = AppTheme.Dark;

    public static event Action<AppTheme>? ThemeChanged;

    static ThemeManager()
    {
        LoadThemePreference();
    }

    public static void Initialize()
    {
        ApplyTheme(CurrentTheme);
        if (!File.Exists(SettingsFile))
        {
            SaveThemePreference();
        }
    }

    public static void SetTheme(AppTheme theme)
    {
        CurrentTheme = theme;
        ApplyTheme(theme);
        SaveThemePreference();
        ThemeChanged?.Invoke(CurrentTheme);
    }

    public static void ToggleTheme()
    {
        SetTheme(CurrentTheme == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark);
    }

    public static void ApplyTheme(AppTheme theme)
    {
        if (WpfApplication.Current == null) return;

        var res = WpfApplication.Current.Resources;

        if (theme == AppTheme.Dark)
        {
            // === TEMA OSCURO 100% PURO (Samsung One UI / Slate Dark) ===
            res["ToolbarBackground"] = new SolidColorBrush(MediaColor.FromRgb(15, 23, 42));       // #0F172A
            res["ToolbarBorder"] = new SolidColorBrush(MediaColor.FromRgb(51, 65, 85));           // #334155
            res["ToolbarHover"] = new SolidColorBrush(MediaColor.FromRgb(30, 41, 59));            // #1E293B
            res["ToolbarPressed"] = new SolidColorBrush(MediaColor.FromRgb(15, 23, 42));          // #0F172A
            res["ToolbarText"] = new SolidColorBrush(MediaColor.FromRgb(248, 250, 252));          // #F8FAFC
            res["ToolbarSeparator"] = new SolidColorBrush(MediaColor.FromRgb(51, 65, 85));        // #334155

            res["MenuBackground"] = new SolidColorBrush(MediaColor.FromRgb(15, 23, 42));          // #0F172A
            res["MenuBorder"] = new SolidColorBrush(MediaColor.FromRgb(51, 65, 85));              // #334155
            res["MenuForeground"] = new SolidColorBrush(MediaColor.FromRgb(248, 250, 252));       // #F8FAFC
            res["MenuHover"] = new SolidColorBrush(MediaColor.FromRgb(30, 41, 59));               // #1E293B
            res["MenuSeparator"] = new SolidColorBrush(MediaColor.FromRgb(51, 65, 85));           // #334155

            res["WindowBackground"] = new SolidColorBrush(MediaColor.FromRgb(9, 13, 22));         // #090D16
            res["CardBackground"] = new SolidColorBrush(MediaColor.FromRgb(15, 23, 42));          // #0F172A
            res["CardBorder"] = new SolidColorBrush(MediaColor.FromRgb(51, 65, 85));              // #334155
            res["ActionBtnBackground"] = new SolidColorBrush(MediaColor.FromRgb(30, 41, 59));     // #1E293B
            res["ActionBtnHover"] = new SolidColorBrush(MediaColor.FromRgb(51, 65, 85));          // #334155
            res["PrimaryText"] = new SolidColorBrush(MediaColor.FromRgb(248, 250, 252));          // #F8FAFC
            res["SecondaryText"] = new SolidColorBrush(MediaColor.FromRgb(148, 163, 184));       // #94A3B8
        }
        else
        {
            // === TEMA CLARO 100% PURO (Limpio, Blanco y Slate) ===
            res["ToolbarBackground"] = new SolidColorBrush(MediaColor.FromRgb(255, 255, 255));    // #FFFFFF
            res["ToolbarBorder"] = new SolidColorBrush(MediaColor.FromRgb(226, 232, 240));        // #E2E8F0
            res["ToolbarHover"] = new SolidColorBrush(MediaColor.FromRgb(241, 245, 249));         // #F1F5F9
            res["ToolbarPressed"] = new SolidColorBrush(MediaColor.FromRgb(203, 213, 225));       // #CBD5E1
            res["ToolbarText"] = new SolidColorBrush(MediaColor.FromRgb(15, 23, 42));             // #0F172A
            res["ToolbarSeparator"] = new SolidColorBrush(MediaColor.FromRgb(226, 232, 240));     // #E2E8F0

            res["MenuBackground"] = new SolidColorBrush(MediaColor.FromRgb(255, 255, 255));       // #FFFFFF
            res["MenuBorder"] = new SolidColorBrush(MediaColor.FromRgb(226, 232, 240));           // #E2E8F0
            res["MenuForeground"] = new SolidColorBrush(MediaColor.FromRgb(15, 23, 42));          // #0F172A
            res["MenuHover"] = new SolidColorBrush(MediaColor.FromRgb(241, 245, 249));            // #F1F5F9
            res["MenuSeparator"] = new SolidColorBrush(MediaColor.FromRgb(226, 232, 240));        // #E2E8F0

            res["WindowBackground"] = new SolidColorBrush(MediaColor.FromRgb(248, 250, 252));     // #F8FAFC
            res["CardBackground"] = new SolidColorBrush(MediaColor.FromRgb(255, 255, 255));       // #FFFFFF
            res["CardBorder"] = new SolidColorBrush(MediaColor.FromRgb(226, 232, 240));           // #E2E8F0
            res["ActionBtnBackground"] = new SolidColorBrush(MediaColor.FromRgb(241, 245, 249));  // #F1F5F9
            res["ActionBtnHover"] = new SolidColorBrush(MediaColor.FromRgb(226, 232, 240));       // #E2E8F0
            res["PrimaryText"] = new SolidColorBrush(MediaColor.FromRgb(15, 23, 42));             // #0F172A
            res["SecondaryText"] = new SolidColorBrush(MediaColor.FromRgb(100, 116, 139));        // #64748B
        }
    }

    private static void LoadThemePreference()
    {
        try
        {
            if (File.Exists(SettingsFile))
            {
                string json = File.ReadAllText(SettingsFile);
                var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("Theme", out var prop))
                {
                    string themeStr = prop.GetString() ?? "Dark";
                    if (Enum.TryParse<AppTheme>(themeStr, true, out var theme))
                    {
                        CurrentTheme = theme;
                        return;
                    }
                }
            }
        }
        catch { }

        CurrentTheme = AppTheme.Dark; // Default elegante unificado
    }

    private static void SaveThemePreference()
    {
        try
        {
            string dir = Path.GetDirectoryName(SettingsFile)!;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            string json = JsonSerializer.Serialize(new { Theme = CurrentTheme.ToString() });
            File.WriteAllText(SettingsFile, json);
        }
        catch { }
    }
}
