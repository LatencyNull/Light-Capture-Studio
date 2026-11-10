using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using LightCapture.Core.Models;

namespace LightCapture.Core.Services;

/// <summary>
/// Servicio de traducción rápida y robusta utilizando el motor oficial de traducción de Chrome (dict-chrome-ex).
/// Soporta traducción en lote (batch) de múltiples bloques de texto en una sola llamada HTTP POST,
/// evitando bloqueos de tasa (429 Too Many Requests) y garantizando alta disponibilidad.
/// </summary>
public class GoogleTranslationService : ITranslationService
{
    private static readonly HttpClient _httpClient = new(new HttpClientHandler
    {
        AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
    })
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    static GoogleTranslationService()
    {
        _httpClient.DefaultRequestHeaders.UserAgent.Clear();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
        _httpClient.DefaultRequestHeaders.Accept.ParseAdd("*/*");
    }

    public async Task<TranslationResult> TranslateAsync(string text, string targetLang = "es", string sourceLang = "auto")
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new TranslationResult(text ?? string.Empty, string.Empty, sourceLang, targetLang, IsSuccess: true);
        }

        var batch = await TranslateBatchAsync(new[] { text }, targetLang, sourceLang);
        if (batch.Count > 0)
        {
            return batch[0];
        }

        return new TranslationResult(text, text, sourceLang, targetLang, IsSuccess: false, ErrorMessage: "Empty response");
    }

    public async Task<IReadOnlyList<TranslationResult>> TranslateBatchAsync(
        IReadOnlyList<string> texts,
        string targetLang = "es",
        string sourceLang = "auto")
    {
        if (texts == null || texts.Count == 0)
        {
            return Array.Empty<TranslationResult>();
        }

        targetLang = TranslationLanguageManager.NormalizeLanguage(targetLang);
        sourceLang = string.IsNullOrWhiteSpace(sourceLang) ? "auto" : sourceLang.Trim().ToLowerInvariant();

        // 1. Intentar endpoint oficial rápido de Chrome Extension (dict-chrome-ex)
        try
        {
            var kvps = new List<KeyValuePair<string, string>>();
            foreach (var t in texts)
            {
                kvps.Add(new KeyValuePair<string, string>("q", t ?? string.Empty));
            }

            using var formContent = new FormUrlEncodedContent(kvps);
            string url = $"https://clients5.google.com/translate_a/t?client=dict-chrome-ex&sl={sourceLang}&tl={targetLang}";

            using var response = await _httpClient.PostAsync(url, formContent);
            if (response.IsSuccessStatusCode)
            {
                string json = await response.Content.ReadAsStringAsync();
                var parsedResults = ParseDictChromeResponse(json, texts, targetLang, sourceLang);
                if (parsedResults != null && parsedResults.Count == texts.Count)
                {
                    return parsedResults;
                }
            }
        }
        catch
        {
            // Fallback a endpoints alternativos
        }

        // 2. Fallback secundario: MyMemory Free Translation API para cada texto
        var fallbackResults = new List<TranslationResult>();
        foreach (var text in texts)
        {
            var singleResult = await TranslateWithMyMemoryFallbackAsync(text, targetLang, sourceLang);
            fallbackResults.Add(singleResult);
        }

        return fallbackResults;
    }

    /// <summary>
    /// Parsea la respuesta JSON devuelta por dict-chrome-ex:
    /// [ ["Texto traducido 1", "en"], ["Texto traducido 2", "en"], ... ]
    /// </summary>
    public static IReadOnlyList<TranslationResult>? ParseDictChromeResponse(
        string json,
        IReadOnlyList<string> originalTexts,
        string targetLang,
        string sourceLang)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.String)
            {
                string original = originalTexts.Count > 0 ? originalTexts[0] : string.Empty;
                return new[]
                {
                    new TranslationResult(original, root.GetString() ?? original, sourceLang, targetLang, IsSuccess: true)
                };
            }

            if (root.ValueKind == JsonValueKind.Array)
            {
                int count = root.GetArrayLength();
                var results = new List<TranslationResult>(count);

                for (int i = 0; i < count; i++)
                {
                    var item = root[i];
                    string original = i < originalTexts.Count ? originalTexts[i] : string.Empty;
                    string translated = original;
                    string detectedLang = sourceLang;

                    if (item.ValueKind == JsonValueKind.String)
                    {
                        translated = item.GetString() ?? original;
                    }
                    else if (item.ValueKind == JsonValueKind.Array && item.GetArrayLength() > 0)
                    {
                        translated = item[0].GetString() ?? original;
                        if (item.GetArrayLength() > 1)
                        {
                            detectedLang = item[1].GetString() ?? sourceLang;
                        }
                    }

                    results.Add(new TranslationResult(
                        OriginalText: original,
                        TranslatedText: translated,
                        DetectedSourceLanguage: detectedLang,
                        TargetLanguage: targetLang,
                        IsSuccess: true
                    ));
                }

                return results;
            }
        }
        catch
        {
            // Retorna null si la respuesta no es parseable
        }

        return null;
    }

    /// <summary>
    /// Parsea la matriz de respuesta JSON legacy de translate.googleapis.com client=gtx
    /// [[[ "Traducción segmento 1", "Original 1", ...], ...]]
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

    private static async Task<TranslationResult> TranslateWithMyMemoryFallbackAsync(
        string text,
        string targetLang,
        string sourceLang)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new TranslationResult(text ?? string.Empty, string.Empty, sourceLang, targetLang, IsSuccess: true);
        }

        try
        {
            string from = (sourceLang == "auto" || string.IsNullOrWhiteSpace(sourceLang)) ? "en" : sourceLang;
            string url = $"https://api.mymemory.translated.net/get?q={Uri.EscapeDataString(text)}&langpair={from}|{targetLang}";

            using var res = await _httpClient.GetAsync(url);
            if (res.IsSuccessStatusCode)
            {
                string json = await res.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("responseData", out var responseData) &&
                    responseData.TryGetProperty("translatedText", out var translatedProp))
                {
                    string translated = translatedProp.GetString() ?? text;
                    return new TranslationResult(text, translated, sourceLang, targetLang, IsSuccess: true);
                }
            }
        }
        catch
        {
            // Ignorar y retornar original
        }

        return new TranslationResult(text, text, sourceLang, targetLang, IsSuccess: false, ErrorMessage: "Fallback translation unavailable");
    }
}
