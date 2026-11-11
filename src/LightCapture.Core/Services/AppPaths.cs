using System.IO;

namespace LightCapture.Core.Services;

/// <summary>
/// Centraliza nombres de marca, rutas de datos de usuario y migración transparente desde versiones anteriores.
/// </summary>
public static class AppPaths
{
    public const string AppDisplayName = "Master Capture";
    public const string AppInternalName = "MasterCapture";
    public const string CapturesFolderName = "Master Capture";

    private const string LegacyCapturesMigratedMarker = ".legacy_captures_migrated";

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
    /// Retorna la ruta del archivo de configuración en AppData\Roaming\MasterCapture,
    /// migrando automáticamente si existe un archivo equivalente previo en LightCaptureStudio.
    /// </summary>
    public static string GetRoamingSettingsFile(string fileName)
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string currentDir = Path.Combine(appData, AppInternalName);
        Directory.CreateDirectory(currentDir);

        string targetPath = Path.Combine(currentDir, fileName);
        if (!File.Exists(targetPath))
        {
            string legacyPath = Path.Combine(appData, "LightCaptureStudio", fileName);
            if (File.Exists(legacyPath))
            {
                try
                {
                    File.Copy(legacyPath, targetPath, overwrite: false);
                }
                catch { }
            }
        }

        return targetPath;
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
    /// Retorna la ruta del archivo de metadatos history.json en AppData\Local\MasterCapture,
    /// migrando automáticamente desde rutas previas (LightCapture / Documents) si no existe aún.
    /// </summary>
    public static string GetHistoryMetadataFile(string capturesDir)
    {
        string localDir = GetLocalDataDirectory();
        string targetFile = Path.Combine(localDir, "history.json");

        if (!File.Exists(targetFile))
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string[] legacyMetadataCandidates =
            {
                Path.Combine(localAppData, "LightCapture", "history.json"),
                Path.Combine(capturesDir, "history.json"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Light Capture", "history.json")
            };

            foreach (var candidate in legacyMetadataCandidates)
            {
                if (File.Exists(candidate))
                {
                    try
                    {
                        File.Copy(candidate, targetFile, overwrite: false);
                        break;
                    }
                    catch { }
                }
            }
        }

        return targetFile;
    }

    /// <summary>
    /// Migra capturas de versiones previas (Light Capture / Light Capture Studio) una sola vez,
    /// usando un archivo marcador para que capturas borradas posteriormente no resuciten al reiniciar.
    /// </summary>
    public static void MigrateLegacyCapturesOnce(string targetCapturesDir)
    {
        try
        {
            string markerPath = Path.Combine(GetLocalDataDirectory(), LegacyCapturesMigratedMarker);
            if (File.Exists(markerPath))
            {
                return;
            }

            Directory.CreateDirectory(targetCapturesDir);

            string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            var legacyDirs = new[]
            {
                Path.Combine(docs, "Light Capture"),
                Path.Combine(docs, "Light Capture Studio"),
                Path.Combine(localAppData, "LightCaptureTranslator", "History"),
                Path.Combine(localAppData, "LightCaptureStudio", "History")
            };

            foreach (var oldDir in legacyDirs)
            {
                if (Directory.Exists(oldDir) && !oldDir.Equals(targetCapturesDir, StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var file in Directory.GetFiles(oldDir))
                    {
                        string fileName = Path.GetFileName(file);
                        if (fileName.Equals("history.json", StringComparison.OrdinalIgnoreCase))
                        {
                            continue; // Jamás copiar archivos JSON técnicos a la carpeta de capturas del usuario
                        }

                        string dest = Path.Combine(targetCapturesDir, fileName);
                        if (!File.Exists(dest))
                        {
                            File.Copy(file, dest, overwrite: false);
                        }
                    }
                }
            }

            // Asegurar que no quede ningún history.json técnico dentro de la carpeta de capturas
            string lingeringInDocs = Path.Combine(targetCapturesDir, "history.json");
            if (File.Exists(lingeringInDocs))
            {
                File.Delete(lingeringInDocs);
            }

            File.WriteAllText(markerPath, DateTime.UtcNow.ToString("O"));
        }
        catch { }
    }
}
