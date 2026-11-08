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

    static CloudUploadService()
    {
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
    }

    public async Task<string?> UploadImageAsync(byte[] imageBytes, string fileName = "capture.png")
    {
        if (imageBytes == null || imageBytes.Length == 0) return null;

        // Intento 1: FreeImage.host (API pública estándar de alta velocidad y retención permanente)
        try
        {
            string base64 = Convert.ToBase64String(imageBytes);
            using var form = new MultipartFormDataContent();
            form.Add(new StringContent("6d207e02198a847aa98d0a2a901485a5"), "key");
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
        catch
        {
            // Fallback a siguientes servicios
        }

        // Intento 2: Tmpfiles.org
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
        catch
        {
            // Siguiente fallback
        }

        // Intento 3: Litterbox Catbox (24 horas)
        try
        {
            using var form = new MultipartFormDataContent();
            form.Add(new StringContent("fileupload"), "reqtype");
            form.Add(new StringContent("24h"), "time");

            var fileContent = new ByteArrayContent(imageBytes);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            form.Add(fileContent, "fileToUpload", fileName);

            var response = await _httpClient.PostAsync("https://litterbox.catbox.moe/resources/internals/api.php", form);
            if (response.IsSuccessStatusCode)
            {
                string result = (await response.Content.ReadAsStringAsync()).Trim();
                if (result.StartsWith("http://") || result.StartsWith("https://"))
                {
                    return result;
                }
            }
        }
        catch
        {
            // Error general de red
        }

        return null;
    }
}
