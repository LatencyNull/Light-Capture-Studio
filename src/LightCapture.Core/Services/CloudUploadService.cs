using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace LightCapture.Core.Services;

public class CloudUploadService : ICloudUploadService
{
    private static readonly HttpClient _httpClient = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    private static readonly string SettingsFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "LightCaptureStudio",
        "cloud_settings.json"
    );

    public string? FreeImageApiKey { get; set; }

    static CloudUploadService()
    {
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
    }

    public CloudUploadService()
    {
        LoadApiKey();
    }

    private void LoadApiKey()
    {
        // 1. Prioridad: Variable de entorno del sistema
        FreeImageApiKey = Environment.GetEnvironmentVariable("LIGHTCAPTURE_FREEIMAGE_KEY");

        // 2. Archivo de configuración local del usuario
        if (string.IsNullOrWhiteSpace(FreeImageApiKey))
        {
            try
            {
                if (File.Exists(SettingsFile))
                {
                    string json = File.ReadAllText(SettingsFile);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("freeimage_key", out var keyElem))
                    {
                        FreeImageApiKey = keyElem.GetString();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CloudUploadService] Error reading settings: {ex.Message}");
            }
        }
    }

    public void SaveApiKey(string key)
    {
        FreeImageApiKey = key;
        try
        {
            string dir = Path.GetDirectoryName(SettingsFile)!;
            Directory.CreateDirectory(dir);
            var data = new { freeimage_key = key };
            File.WriteAllText(SettingsFile, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CloudUploadService] Error saving settings: {ex.Message}");
        }
    }

    public async Task<string?> UploadImageAsync(byte[] imageBytes, string fileName = "capture.png")
    {
        if (imageBytes == null || imageBytes.Length == 0) return null;

        // Intento 1: FreeImage.host (si el usuario ha provisto una clave API)
        if (!string.IsNullOrWhiteSpace(FreeImageApiKey))
        {
            try
            {
                string base64 = Convert.ToBase64String(imageBytes);
                using var form = new MultipartFormDataContent();
                form.Add(new StringContent(FreeImageApiKey), "key");
                form.Add(new StringContent("upload"), "action");
                form.Add(new StringContent(base64), "source");
                form.Add(new StringContent("json"), "format");

                var response = await _httpClient.PostAsync("https://freeimage.host/api/1/upload", form);
                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("image", out var imgElem) &&
                        imgElem.TryGetProperty("url", out var urlElem))
                    {
                        string? url = urlElem.GetString();
                        if (!string.IsNullOrEmpty(url)) return url;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CloudUploadService] FreeImage upload failed: {ex.Message}");
            }
        }

        // Intento 2: Tmpfiles.org (almacenamiento temporal para compartir rápido)
        try
        {
            using var form = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(imageBytes);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            form.Add(fileContent, "file", fileName);

            var response = await _httpClient.PostAsync("https://tmpfiles.org/api/v1/upload", form);
            if (response.IsSuccessStatusCode)
            {
                string json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("data", out var dataElem) &&
                    dataElem.TryGetProperty("url", out var urlElem))
                {
                    string rawUrl = urlElem.GetString() ?? string.Empty;
                    if (rawUrl.Contains("tmpfiles.org/"))
                    {
                        return rawUrl.Replace("tmpfiles.org/", "tmpfiles.org/dl/");
                    }
                    return rawUrl;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CloudUploadService] Tmpfiles upload failed: {ex.Message}");
        }

        return null;
    }
}
