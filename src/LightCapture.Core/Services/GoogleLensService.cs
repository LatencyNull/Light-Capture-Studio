using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace LightCapture.Core.Services;

/// <summary>
/// Servicio de integración directa con Google Lens sin intermediarios ni dependencias de terceros.
/// Realiza el upload multipart/form-data directamente al endpoint oficial de Google Lens
/// y captura la redirección 303 (See Other) para obtener la URL interactiva de resultados de búsqueda visual.
/// </summary>
public static class GoogleLensService
{
    private static readonly HttpClientHandler _handler = new HttpClientHandler
    {
        AllowAutoRedirect = false
    };

    private static readonly HttpClient _httpClient = new HttpClient(_handler)
    {
        Timeout = TimeSpan.FromSeconds(12)
    };

    static GoogleLensService()
    {
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
    }

    /// <summary>
    /// Construye la URL directa de búsqueda en Google Lens a partir del enlace público de una imagen.
    /// Al abrirse en el navegador del usuario, Google procesa la imagen y muestra los resultados directamente.
    /// </summary>
    public static string BuildLensSearchUrl(string publicImageUrl)
    {
        return $"https://lens.google.com/uploadbyurl?url={Uri.EscapeDataString(publicImageUrl)}";
    }

    /// <summary>
    /// Construye la URL clásica de búsqueda inversa por imagen de Google.
    /// </summary>
    public static string BuildGoogleImagesSearchUrl(string publicImageUrl)
    {
        return $"https://www.google.com/searchbyimage?image_url={Uri.EscapeDataString(publicImageUrl)}";
    }

    /// <summary>
    /// Sube directamente la imagen a Google Lens y retorna la URL con los resultados de búsqueda visual.
    /// Si ocurre algún problema de red o respuesta inesperada, retorna null.
    /// </summary>
    public static async Task<string?> UploadAndGetSearchUrlAsync(byte[] imageBytes, string fileName = "screenshot.png")
    {
        if (imageBytes == null || imageBytes.Length == 0) return null;

        try
        {
            using var form = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(imageBytes);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            form.Add(fileContent, "encoded_image", fileName);

            var response = await _httpClient.PostAsync("https://lens.google.com/v3/upload", form);

            // Google Lens responde con código 303 (See Other) o redirección y la URL en 'Location'
            if ((int)response.StatusCode >= 300 && (int)response.StatusCode < 400 && response.Headers.Location != null)
            {
                var targetUri = response.Headers.Location;
                if (!targetUri.IsAbsoluteUri)
                {
                    targetUri = new Uri(new Uri("https://lens.google.com/"), targetUri);
                }
                return targetUri.ToString();
            }

            if (response.IsSuccessStatusCode)
            {
                return "https://lens.google.com/";
            }
        }
        catch
        {
            // Error de red, timeout o firewall
        }

        return null;
    }
}
