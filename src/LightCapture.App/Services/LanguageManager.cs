using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using LightCapture.Core.Services;
using WpfApplication = System.Windows.Application;

namespace LightCapture.App.Services;

public enum AppLanguage
{
    Spanish,
    English
}

public static class LanguageManager
{
    private static readonly string SettingsFile = AppPaths.GetRoamingSettingsFile("ui_language.json");

    public static AppLanguage CurrentLanguage { get; private set; } = AppLanguage.Spanish;

    public static event Action<AppLanguage>? LanguageChanged;

    private static readonly Dictionary<string, string> SpanishStrings = new(StringComparer.Ordinal)
    {
        // Tray
        ["Tray_Tooltip"] = "Master Capture - {0} para capturar pantalla",
        ["Menu_ScreenCapture"] = "Captura de pantalla",
        ["Menu_TakeCapture"] = "Tomar captura",
        ["Menu_History"] = "Historial de capturas...",
        ["Menu_OpenFolder"] = "Abrir carpeta de capturas",
        ["Menu_HotkeyConfig"] = "Configurar tecla de captura ({0})...",
        ["Menu_ThemeDarkToLight"] = "Tema: Oscuro (Cambiar a Claro)",
        ["Menu_ThemeLightToDark"] = "Tema: Claro (Cambiar a Oscuro)",
        ["Menu_UiLanguage"] = "Idioma de la interfaz",
        ["Menu_OcrLanguage"] = "Idioma de traducción (Lens / OCR)",
        ["Menu_Exit"] = "Salir",
        ["Lang_Spanish"] = "🇪🇸  Español",
        ["Lang_English"] = "🇺🇸  English",

        // Toolbar
        ["Tool_Pen"] = "Pluma de dibujo [✏️]",
        ["Tool_Marker"] = "Marcador fluorescente [🖍️]",
        ["Tool_Arrow"] = "Flecha indicadora [↗️]",
        ["Tool_Rect"] = "Marco rectangular [🔲]",
        ["Tool_Redact"] = "Censura segura Knox [🔒] (Bloque sólido irreversible)\nClic derecho para pixelado mosaico",
        ["Tool_Text"] = "Añadir texto [🔤]",
        ["Tool_Color"] = "Color del trazo [🎨]",
        ["Tool_Undo"] = "Deshacer trazo [↩️ Ctrl+Z]",
        ["Tool_Share"] = "Compartir captura [📤]\nWhatsApp, Facebook, Instagram, Email, Telegram, X, Nube...",
        ["Tool_Translate"] = "Galaxy AI Traducir [🌐 Ctrl+T]\nDestino: {0} (Clic para traducir / alternar original)\nClic derecho: Cambiar idioma o copiar",
        ["Tool_Lens"] = "Buscar imagen con Google Lens [🔍]",
        ["Tool_Gemini"] = "Abrir en Gemini [✨ Ctrl+G]\nCopia la captura y abre gemini.google.com (Pega con Ctrl+V)\nClic derecho: Opciones de Gemini",
        ["Tool_Record"] = "Grabar pantalla (MP4 o GIF) [🎞️]\nClic: Opciones de grabación para elegir audio y empezar",
        ["Tool_Print"] = "Imprimir captura [🖨️ Ctrl+P]",
        ["Tool_CopyOcr"] = "Copiar texto con OCR [📋 Ctrl+Shift+T]",
        ["Tool_Copy"] = "Copiar imagen al portapapeles [📑 Ctrl+C]",
        ["Tool_Save"] = "Guardar imagen en disco [💾 Ctrl+S]",
        ["Tool_Cancel"] = "Cancelar selección [❌ Esc]",

        // Share Context Menu
        ["Share_WhatsAppApp"] = "WhatsApp (App de Windows)",
        ["Share_WhatsAppWeb"] = "WhatsApp Web",
        ["Share_Telegram"] = "Telegram Web",
        ["Share_Twitter"] = "X (Twitter)",
        ["Share_Facebook"] = "Facebook Web",
        ["Share_Instagram"] = "Instagram Web",
        ["Share_Email"] = "Enviar por Correo (Email)",
        ["Share_Cloud"] = "Subir a la nube (Enlace público instantáneo)",
        ["Share_OptionsHeader"] = "OPCIONES DE COMPARTIR",

        // Gemini Context Menu
        ["Gemini_Direct"] = "Abrir Gemini directamente",
        ["Gemini_Explain"] = "Explicar esta imagen",
        ["Gemini_ExtractCode"] = "Extraer código de esta imagen",
        ["Gemini_SolveMath"] = "Resolver ejercicio / problema matemático",
        ["Gemini_Summary"] = "Resumir este contenido",
        ["Gemini_AskImage"] = "Preguntar sobre la imagen en Gemini (Pegar con Ctrl+V)",
        ["Gemini_AskText"] = "Preguntar sobre el texto en Gemini (OCR + Prompt)",

        // Recording & Audio Context Menu
        ["Rec_FullAudio"] = "🎙️ + 🔊 Grabar con Micrófono y Audio del Sistema",
        ["Rec_MicOnly"] = "🎙️ Solo Micrófono (Voz / Narración)",
        ["Rec_SysAudioOnly"] = "🔊 Solo Audio del Sistema",
        ["Rec_Muted"] = "🔇 Sin sonido (Mudo)",

        // Translation Context Menu
        ["Trans_CopyTranslated"] = "📋  Copiar texto traducido",
        ["Trans_CopyOcr"] = "📝  Copiar texto original (OCR)",

        // History Window
        ["History_Title"] = "Historial de Capturas - Master Capture",
        ["History_Header"] = "🕒 Historial de Capturas",
        ["History_CapturesCount"] = "{0} capturas",
        ["History_SearchPlaceholder"] = "🔍 Buscar por palabras detectadas en pantalla...",
        ["History_Incognito"] = "Modo Incógnito",
        ["History_IncognitoTooltip"] = "Pausa el guardado de capturas en disco para privacidad",
        ["History_SelectAll"] = "Seleccionar todo",
        ["History_SelectAllTooltip"] = "Seleccionar o deseleccionar todas las capturas visibles",
        ["History_DeleteSelected"] = "🗑️ Eliminar seleccionadas",
        ["History_DeleteSelectedTooltip"] = "Eliminar capturas seleccionadas del disco y del historial",
        ["History_SelectForBatchDelete"] = "Seleccionar para borrado múltiple",
        ["History_OpenFolder"] = "📁 Abrir carpeta",
        ["History_OpenFolderTooltip"] = "Abrir carpeta de capturas en el Explorador de Windows",
        ["History_ClearAll"] = "Vaciar todo",
        ["History_Close"] = "Cerrar",
        ["History_CopyImg"] = "Copiar Img",
        ["History_CopyTxt"] = "Copiar Txt",
        ["History_EmptyTitle"] = "No hay capturas en el historial todavía",
        ["History_EmptyDesc"] = "Tus capturas se indexan y guardan aquí automáticamente al copiar o guardar.",
        ["History_EmptyBtn"] = "📁 Abrir carpeta de capturas",
        ["History_Footer"] = "📁 Las capturas se guardan automáticamente en tu carpeta de capturas. Privacidad: 100% local en tu equipo.",
        ["History_ClearConfirm"] = "¿Seguro que deseas vaciar todo el historial de capturas?",
        ["History_ClearConfirmTitle"] = "Vaciar Historial",

        // Hotkey Window
        ["Hotkey_Title"] = "Configurar Tecla de Captura - Master Capture",
        ["Hotkey_Header"] = "Tecla de Captura de Pantalla",
        ["Hotkey_Desc"] = "Personaliza el botón o combinación global para iniciar la captura en cualquier momento.",
        ["Hotkey_Current"] = "TECLA ASIGNADA ACTUALMENTE",
        ["Hotkey_Instruction"] = "👉 Haz clic aquí y presiona cualquier tecla de tu teclado",
        ["Hotkey_Presets"] = "O selecciona un atajo popular de acceso rápido:",
        ["Hotkey_PresetDefault"] = "F9 (Por defecto)",
        ["Hotkey_PresetPrintScreen"] = "Impr Pant",
        ["Hotkey_Reset"] = "Restablecer F9",
        ["Hotkey_Cancel"] = "Cancelar",
        ["Hotkey_Save"] = "Guardar y Aplicar",
        ["Hotkey_Listening"] = "👉 Presiona cualquier tecla...",

        // Video Preview Window
        ["Video_Title"] = "Vista Previa de Video - Master Capture",
        ["Video_GifTitle"] = "Vista Previa de GIF - Master Capture",
        ["Video_Header"] = "Vista Previa del Clip",
        ["Video_Trim"] = "✂️ Recortar",
        ["Video_Save"] = "💾 Guardar",
        ["Video_Copy"] = "📑 Copiar",
        ["Video_ExportGif"] = "🎞️ Exportar GIF",
        ["Video_Close"] = "Cerrar",
        ["Video_CopyClip"] = "Copiar Clip",
        ["Video_CopyClipTooltip"] = "Copiar video o GIF al portapapeles (Ctrl+V en cualquier app)",
        ["Video_SaveClip"] = "Guardar...",
        ["Video_SaveClipTooltip"] = "Guardar archivo en disco (Ctrl+S)",
        ["Video_Discard"] = "Descartar",
        ["Video_DiscardTooltip"] = "Descartar este clip",
        ["Video_Share"] = "Compartir ▾",
        ["Video_PlayPauseTooltip"] = "Reproducir / Pausar (Espacio)",
        ["Video_Timeline"] = "Línea de tiempo",
        ["Video_TimelineHint"] = "Arrastra los bloques para ajustar aparición y duración",
        ["Video_ObjectsCount"] = "{0} objetos",
        ["Video_ToolArrowTooltip"] = "Flecha indicadora [↗️]",
        ["Video_ToolTextTooltip"] = "Añadir texto [🔤]\nHaz clic en el video para escribir",
        ["Video_ToolPenTooltip"] = "Pluma libre [✏️]",
        ["Video_ToolRectTooltip"] = "Marco rectangular [🔲]",
        ["Video_ToolRedactTooltip"] = "Censura Knox [🔒]\nBloque opaco para ocultar información privada",
        ["Video_ToolColorTooltip"] = "Color de anotación [🎨]",
        ["Video_ToolUndoTooltip"] = "Deshacer trazo [↩️ Ctrl+Z]",
        ["Video_ToolClearTooltip"] = "Limpiar todas las anotaciones [🗑️]",

        // Recording Widget
        ["Rec_Stop"] = "Detener y guardar grabación",
        ["Rec_Pause"] = "Pausar / Reanudar grabación",
        ["Rec_Discard"] = "Descartar y cancelar grabación",

        // Toasts
        ["Toast_TextCopied"] = "Texto extraído copiado al portapapeles 📋",
        ["Toast_ImageCopied"] = "Captura copiada al portapapeles 📑",
        ["Toast_Saved"] = "Imagen guardada e indexada en el historial 💾",
        ["Toast_Uploading"] = "Subiendo captura a la nube...",
        ["Toast_CloudCopied"] = "¡Enlace copiado al portapapeles! ☁️",
        ["Toast_NoText"] = "No se detectó texto en la selección.",
        ["Toast_OcrRunning"] = "Extrayendo texto con OCR...",
        ["Toast_Translating"] = "Traduciendo a {0}...",
        ["Toast_TranslateDone"] = "¡Traducción al {0} lista! 🌐 (Copiada al portapapeles)",
        ["Toast_LensSearch"] = "Buscando en Google Lens... 🔍",
        ["Toast_GeminiCopied"] = "¡Captura copiada! Pega con Ctrl+V en Gemini ✨",
        ["Toast_Undo"] = "↩️ Deshacer (Ctrl+Z)",
        ["Toast_NothingToUndo"] = "Nada que deshacer",
        ["Toast_AutoSelected"] = "🧲 Ventana auto-seleccionada",
        ["Toast_QrDetected"] = "¡Código QR detectado en la captura!",
        ["Toast_QrCopied"] = "Contenido QR copiado al portapapeles 📋",
        ["Toast_ReadingAloud"] = "🔊 Leyendo texto en voz alta..."
    };

    private static readonly Dictionary<string, string> EnglishStrings = new(StringComparer.Ordinal)
    {
        // Tray
        ["Tray_Tooltip"] = "Master Capture - {0} to capture screen",
        ["Menu_ScreenCapture"] = "Screen Capture",
        ["Menu_TakeCapture"] = "Capture Screen",
        ["Menu_History"] = "Capture History...",
        ["Menu_OpenFolder"] = "Open Captures Folder",
        ["Menu_HotkeyConfig"] = "Configure Capture Hotkey ({0})...",
        ["Menu_ThemeDarkToLight"] = "Theme: Dark (Switch to Light)",
        ["Menu_ThemeLightToDark"] = "Theme: Light (Switch to Dark)",
        ["Menu_UiLanguage"] = "Interface Language",
        ["Menu_OcrLanguage"] = "Translation Language (Lens / OCR)",
        ["Menu_Exit"] = "Exit",
        ["Lang_Spanish"] = "🇪🇸  Spanish",
        ["Lang_English"] = "🇺🇸  English",

        // Toolbar
        ["Tool_Pen"] = "Drawing Pen [✏️]",
        ["Tool_Marker"] = "Highlighter Marker [🖍️]",
        ["Tool_Arrow"] = "Pointer Arrow [↗️]",
        ["Tool_Rect"] = "Rectangle Frame [🔲]",
        ["Tool_Redact"] = "Knox Redaction [🔒] (Irreversible solid block)\nRight-click for mosaic blur",
        ["Tool_Text"] = "Add Text [🔤]",
        ["Tool_Color"] = "Stroke Color [🎨]",
        ["Tool_Undo"] = "Undo Stroke [↩️ Ctrl+Z]",
        ["Tool_Share"] = "Share capture [📤]\nWhatsApp, Facebook, Instagram, Email, Telegram, X, Cloud...",
        ["Tool_Translate"] = "Galaxy AI Translate [🌐 Ctrl+T]\nTarget: {0} (Click to translate / toggle original)\nRight-click: Change language or copy",
        ["Tool_Lens"] = "Search image with Google Lens [🔍]",
        ["Tool_Gemini"] = "Open in Gemini [✨ Ctrl+G]\nCopies capture and opens gemini.google.com (Paste with Ctrl+V)\nRight-click: Gemini options",
        ["Tool_Record"] = "Record screen (MP4 or GIF) [🎞️]\nClick: Open recording options to choose audio and start",
        ["Tool_Print"] = "Print capture [🖨️ Ctrl+P]",
        ["Tool_CopyOcr"] = "Copy text with OCR [📋 Ctrl+Shift+T]",
        ["Tool_Copy"] = "Copy image to clipboard [📑 Ctrl+C]",
        ["Tool_Save"] = "Save image to disk [💾 Ctrl+S]",
        ["Tool_Cancel"] = "Cancel selection [❌ Esc]",

        // Share Context Menu
        ["Share_WhatsAppApp"] = "WhatsApp (Windows App)",
        ["Share_WhatsAppWeb"] = "WhatsApp Web",
        ["Share_Telegram"] = "Telegram Web",
        ["Share_Twitter"] = "X (Twitter)",
        ["Share_Facebook"] = "Facebook Web",
        ["Share_Instagram"] = "Instagram Web",
        ["Share_Email"] = "Send by Email",
        ["Share_Cloud"] = "Upload to cloud (Instant public link)",
        ["Share_OptionsHeader"] = "SHARE OPTIONS",

        // Gemini Context Menu
        ["Gemini_Direct"] = "Open Gemini directly",
        ["Gemini_Explain"] = "Explain this image",
        ["Gemini_ExtractCode"] = "Extract code from this image",
        ["Gemini_SolveMath"] = "Solve math exercise / problem",
        ["Gemini_Summary"] = "Summarize this content",
        ["Gemini_AskImage"] = "Ask about image in Gemini (Paste with Ctrl+V)",
        ["Gemini_AskText"] = "Ask about text in Gemini (OCR + Prompt)",

        // Recording & Audio Context Menu
        ["Rec_FullAudio"] = "🎙️ + 🔊 Record with Microphone and System Audio",
        ["Rec_MicOnly"] = "🎙️ Microphone only (Voice / Narration)",
        ["Rec_SysAudioOnly"] = "🔊 System Audio only",
        ["Rec_Muted"] = "🔇 Muted (No sound)",

        // Translation Context Menu
        ["Trans_CopyTranslated"] = "📋  Copy translated text",
        ["Trans_CopyOcr"] = "📝  Copy original text (OCR)",

        // History Window
        ["History_Title"] = "Capture History - Master Capture",
        ["History_Header"] = "🕒 Capture History",
        ["History_CapturesCount"] = "{0} captures",
        ["History_SearchPlaceholder"] = "🔍 Search words detected on screen...",
        ["History_Incognito"] = "Incognito Mode",
        ["History_IncognitoTooltip"] = "Pauses saving captures to disk for privacy",
        ["History_SelectAll"] = "Select All",
        ["History_SelectAllTooltip"] = "Select or deselect all visible captures",
        ["History_DeleteSelected"] = "🗑️ Delete Selected",
        ["History_DeleteSelectedTooltip"] = "Delete selected captures from disk and history",
        ["History_SelectForBatchDelete"] = "Select for batch delete",
        ["History_OpenFolder"] = "📁 Open Folder",
        ["History_OpenFolderTooltip"] = "Open captures folder in Windows Explorer",
        ["History_ClearAll"] = "Clear All",
        ["History_Close"] = "Close",
        ["History_CopyImg"] = "Copy Img",
        ["History_CopyTxt"] = "Copy Txt",
        ["History_EmptyTitle"] = "No captures in history yet",
        ["History_EmptyDesc"] = "Your captures are indexed and saved here automatically when copying or saving.",
        ["History_EmptyBtn"] = "📁 Open captures folder",
        ["History_Footer"] = "📁 Captures are automatically saved in your captures folder. Privacy: 100% local on your machine.",
        ["History_ClearConfirm"] = "Are you sure you want to clear all capture history?",
        ["History_ClearConfirmTitle"] = "Clear History",

        // Hotkey Window
        ["Hotkey_Title"] = "Configure Capture Hotkey - Master Capture",
        ["Hotkey_Header"] = "Screen Capture Hotkey",
        ["Hotkey_Desc"] = "Customize the global key or shortcut to start screen capture anytime.",
        ["Hotkey_Current"] = "CURRENTLY ASSIGNED HOTKEY",
        ["Hotkey_Instruction"] = "👉 Click here and press any key on your keyboard",
        ["Hotkey_Presets"] = "Or choose a popular preset shortcut:",
        ["Hotkey_PresetDefault"] = "F9 (Default)",
        ["Hotkey_PresetPrintScreen"] = "Print Screen",
        ["Hotkey_Reset"] = "Reset to F9",
        ["Hotkey_Cancel"] = "Cancel",
        ["Hotkey_Save"] = "Save and Apply",
        ["Hotkey_Listening"] = "👉 Press any key...",

        // Video Preview Window
        ["Video_Title"] = "Clip Preview - Master Capture",
        ["Video_GifTitle"] = "GIF Preview - Master Capture",
        ["Video_Header"] = "Clip Preview",
        ["Video_Trim"] = "✂️ Trim",
        ["Video_Save"] = "💾 Save",
        ["Video_Copy"] = "📑 Copy",
        ["Video_ExportGif"] = "🎞️ Export GIF",
        ["Video_Close"] = "Close",
        ["Video_CopyClip"] = "Copy Clip",
        ["Video_CopyClipTooltip"] = "Copy video or GIF to clipboard (Ctrl+V in any app)",
        ["Video_SaveClip"] = "Save...",
        ["Video_SaveClipTooltip"] = "Save file to disk (Ctrl+S)",
        ["Video_Discard"] = "Discard",
        ["Video_DiscardTooltip"] = "Discard this clip",
        ["Video_Share"] = "Share ▾",
        ["Video_PlayPauseTooltip"] = "Play / Pause (Space)",
        ["Video_Timeline"] = "Timeline",
        ["Video_TimelineHint"] = "Drag blocks to adjust appearance and duration",
        ["Video_ObjectsCount"] = "{0} objects",
        ["Video_ToolArrowTooltip"] = "Pointer Arrow [↗️]",
        ["Video_ToolTextTooltip"] = "Add Text [🔤]\nClick on video to type",
        ["Video_ToolPenTooltip"] = "Freehand Pen [✏️]",
        ["Video_ToolRectTooltip"] = "Rectangle Frame [🔲]",
        ["Video_ToolRedactTooltip"] = "Knox Redaction [🔒]\nSolid opaque block to conceal private info",
        ["Video_ToolColorTooltip"] = "Annotation Color [🎨]",
        ["Video_ToolUndoTooltip"] = "Undo Stroke [↩️ Ctrl+Z]",
        ["Video_ToolClearTooltip"] = "Clear All Annotations [🗑️]",

        // Recording Widget
        ["Rec_Stop"] = "Stop and save recording",
        ["Rec_Pause"] = "Pause / Resume recording",
        ["Rec_Discard"] = "Discard and cancel recording",

        // Toasts
        ["Toast_TextCopied"] = "Extracted text copied to clipboard 📋",
        ["Toast_ImageCopied"] = "Capture copied to clipboard 📑",
        ["Toast_Saved"] = "Image saved and indexed in history 💾",
        ["Toast_Uploading"] = "Uploading capture to cloud...",
        ["Toast_CloudCopied"] = "Link copied to clipboard! ☁️",
        ["Toast_NoText"] = "No text detected in selection.",
        ["Toast_OcrRunning"] = "Extracting text with OCR...",
        ["Toast_Translating"] = "Translating to {0}...",
        ["Toast_TranslateDone"] = "Translation to {0} ready! 🌐 (Copied to clipboard)",
        ["Toast_LensSearch"] = "Searching Google Lens... 🔍",
        ["Toast_GeminiCopied"] = "Capture copied! Paste with Ctrl+V in Gemini ✨",
        ["Toast_Undo"] = "↩️ Undo (Ctrl+Z)",
        ["Toast_NothingToUndo"] = "Nothing to undo",
        ["Toast_AutoSelected"] = "🧲 Window auto-selected",
        ["Toast_QrDetected"] = "QR Code detected in capture!",
        ["Toast_QrCopied"] = "QR content copied to clipboard 📋",
        ["Toast_ReadingAloud"] = "🔊 Reading text aloud..."
    };

    static LanguageManager()
    {
        LoadPreference();
    }

    public static void Initialize()
    {
        ApplyLanguage(CurrentLanguage);
        if (!File.Exists(SettingsFile))
        {
            SavePreference();
        }
    }

    public static void SetLanguage(AppLanguage language)
    {
        CurrentLanguage = language;
        ApplyLanguage(language);
        SavePreference();
        LanguageChanged?.Invoke(CurrentLanguage);
    }

    public static string Get(string key, params object[] args)
    {
        var dict = CurrentLanguage == AppLanguage.Spanish ? SpanishStrings : EnglishStrings;
        if (!dict.TryGetValue(key, out var val))
        {
            // Fallback to Spanish or key itself
            if (!SpanishStrings.TryGetValue(key, out val))
            {
                val = key;
            }
        }

        if (args != null && args.Length > 0)
        {
            try
            {
                return string.Format(val, args);
            }
            catch
            {
                return val;
            }
        }

        return val;
    }

    public static void ApplyLanguage(AppLanguage language)
    {
        CurrentLanguage = language;
        if (WpfApplication.Current == null) return;

        var res = WpfApplication.Current.Resources;
        var dict = language == AppLanguage.Spanish ? SpanishStrings : EnglishStrings;
        foreach (var kvp in dict)
        {
            res[kvp.Key] = kvp.Value;
        }
    }

    private static void LoadPreference()
    {
        try
        {
            if (File.Exists(SettingsFile))
            {
                string json = File.ReadAllText(SettingsFile);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("Language", out var prop))
                {
                    string langStr = prop.GetString() ?? "";
                    if (Enum.TryParse<AppLanguage>(langStr, true, out var lang))
                    {
                        CurrentLanguage = lang;
                        return;
                    }
                }
            }
        }
        catch { }

        // Autodetección del sistema en primer inicio
        try
        {
            string cultureTwoLetter = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
            if (cultureTwoLetter.StartsWith("es"))
            {
                CurrentLanguage = AppLanguage.Spanish;
                return;
            }
        }
        catch { }

        CurrentLanguage = AppLanguage.Spanish; // Español por defecto para usuarios hispanohablantes
    }

    private static void SavePreference()
    {
        try
        {
            string? dir = Path.GetDirectoryName(SettingsFile);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string json = JsonSerializer.Serialize(new { Language = CurrentLanguage.ToString() }, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFile, json);
        }
        catch { }
    }
}
