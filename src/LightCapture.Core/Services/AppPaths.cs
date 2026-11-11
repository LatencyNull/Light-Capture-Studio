using System.IO;

namespace LightCapture.Core.Services;

/// <summary>
/// Centraliza nombres de marca y rutas locales de datos de usuario para Master Capture.
/// </summary>
public static class AppPaths
{
    public const string AppDisplayName = "Master Capture";
    public const string AppInternalName = "MasterCapture";
    public const string CapturesFolderName = "Master Capture";

    /// <summary>
    /// Retorna la carpeta principal de capturas del usuario (Documents\Master Capture) y garantiza que exista.
    /// </summary>
    public static string GetCapturesDirectory()
    {
        string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string dir = Path.Combine(docs, CapturesFolderName);
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>
    /// Retorna la ruta del archivo de configuración en AppData\Roaming\MasterCapture.
    /// </summary>
    public static string GetRoamingSettingsFile(string fileName)
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string currentDir = Path.Combine(appData, AppInternalName);
        Directory.CreateDirectory(currentDir);
        return Path.Combine(currentDir, fileName);
    }

    /// <summary>
    /// Retorna la carpeta de datos locales (AppData\Local\MasterCapture[\subFolder]).
    /// </summary>
    public static string GetLocalDataDirectory(string? subFolder = null)
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string dir = string.IsNullOrWhiteSpace(subFolder)
            ? Path.Combine(localAppData, AppInternalName)
            : Path.Combine(localAppData, AppInternalName, subFolder);

        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>
    /// Retorna el directorio de binarios extraídos del grabador (AppData\Local\MasterCapture\bin).
    /// </summary>
    public static string GetRecorderBinDirectory() => GetLocalDataDirectory("bin");

    /// <summary>
    /// Retorna el directorio temporal para edición y renderizado de clips.
    /// </summary>
    public static string GetTempRecordingsDirectory()
    {
        string dir = Path.Combine(Path.GetTempPath(), "MasterCapture_Recordings");
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>
    /// Retorna la ruta del archivo de metadatos history.json en AppData\Local\MasterCapture.
    /// </summary>
    public static string GetHistoryMetadataFile()
    {
        return Path.Combine(GetLocalDataDirectory(), "history.json");
    }
}
