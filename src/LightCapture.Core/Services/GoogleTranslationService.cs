using System.Net.Http;
using System.Text.Json;
using LightCapture.Core.Models;

namespace LightCapture.Core.Services;

public class GoogleTranslationService : ITranslationService
{
    private static readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(8)
    };

    static GoogleTranslationService()
    {
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
    }

    public async Task<TranslationResult> TranslateAsync(string text, string targetLang = "es", string sourceLang = "auto")
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new TranslationResult(text ?? string.Empty, string.Empty, sourceLang, targetLang);
        }

        try
        {
            string encoded = Uri.EscapeDataString(text);
            string url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl={sourceLang}&tl={targetLang}&dt=t&q={encoded}";

            using var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            string json = await response.Content.ReadAsStringAsync();
            string translated = ParseGtxResponse(json);

            return new TranslationResult(
                OriginalText: text,
                TranslatedText: translated,
                DetectedSourceLanguage: sourceLang,
                TargetLanguage: targetLang,
                IsSuccess: true
            );
        }
        catch (Exception ex)
        {
            return new TranslationResult(
                OriginalText: text,
                TranslatedText: text, // Fallback al texto original si hay error
                DetectedSourceLanguage: sourceLang,
                TargetLanguage: targetLang,
                IsSuccess: false,
                ErrorMessage: ex.Message
            );
        }
    }

    /// <summary>
    /// Parsea la matriz de respuesta JSON de Google Translate:
    /// [[[ "Traducción segmento 1", "Original 1", ...], [ "Traducción segmento 2", ...]], ...]
    /// </summary>
    public static string ParseGtxResponse(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return string.Empty;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
            {
                var sentencesArray = root[0];
                if (sentencesArray.ValueKind == JsonValueKind.Array)
                {
                    var sb = new System.Text.StringBuilder();
                    foreach (var sentence in sentencesArray.EnumerateArray())
                    {
                        if (sentence.ValueKind == JsonValueKind.Array && sentence.GetArrayLength() > 0)
                        {
                            string? part = sentence[0].GetString();
                            if (!string.IsNullOrEmpty(part))
                            {
                                sb.Append(part);
                            }
                        }
                    }
                    return sb.ToString();
                }
            }
        }
        catch
        {
            // Si el parsing falla, retornamos cadena vacía
        }

        return string.Empty;
    }
}
