using LightCapture.Core.Models;
using LightCapture.Core.Services;

namespace LightCapture.Tests;

public class TranslationServiceTests
{
    [Fact]
    public void ParseGtxResponse_SingleSentence_ExtractsCorrectTranslation()
    {
        // Arrange
        // Estructura real devuelta por translate.googleapis.com client=gtx
        string mockJson = "[[[\"Hola mundo\",\"Hello world\",null,null,10]],null,\"en\",null,null,null,null,[]]";

        // Act
        string result = GoogleTranslationService.ParseGtxResponse(mockJson);

        // Assert
        Assert.Equal("Hola mundo", result);
    }

    [Fact]
    public void ParseGtxResponse_MultipleSentences_JoinsCleanly()
    {
        // Arrange
        string mockJson = "[[[\"Primera oración. \",\"First sentence. \",null,null,10],[\"Segunda oración.\",\"Second sentence.\",null,null,10]],null,\"en\"]";

        // Act
        string result = GoogleTranslationService.ParseGtxResponse(mockJson);

        // Assert
        Assert.Equal("Primera oración. Segunda oración.", result);
    }

    [Fact]
    public async Task TranslateAsync_EmptyOrWhitespace_ReturnsEmptyTranslation()
    {
        // Arrange
        var service = new GoogleTranslationService();

        // Act
        var result = await service.TranslateAsync("   ");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(string.Empty, result.TranslatedText);
    }

    [Fact]
    public async Task TranslateAsync_RealEndpoint_TranslatesEnglishToSpanish()
    {
        // Arrange
        var service = new GoogleTranslationService();

        // Act
        var result = await service.TranslateAsync("Screen capture with instant translation", targetLang: "es", sourceLang: "auto");

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsSuccess, $"Failed: {result.ErrorMessage}");
        Assert.Contains("traducci", result.TranslatedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TranslationLanguageManager_DefaultsToSpanish()
    {
        // Assert: only Spanish and English by default, starting in Spanish
        TranslationLanguageManager.SetLanguage(TranslationLanguageManager.Spanish);
        Assert.Equal("es", TranslationLanguageManager.CurrentLanguage);
        Assert.Equal("ES", TranslationLanguageManager.CurrentLanguageBadge);
        Assert.Equal("Spanish", TranslationLanguageManager.CurrentLanguageDisplayName);
    }

    [Fact]
    public void TranslationLanguageManager_SetToEnglish_UpdatesLanguageAndBadge()
    {
        // Act
        TranslationLanguageManager.SetLanguage(TranslationLanguageManager.English);

        // Assert
        Assert.Equal("en", TranslationLanguageManager.CurrentLanguage);
        Assert.Equal("EN", TranslationLanguageManager.CurrentLanguageBadge);
        Assert.Equal("English", TranslationLanguageManager.CurrentLanguageDisplayName);

        // Cleanup: return to Spanish
        TranslationLanguageManager.SetLanguage(TranslationLanguageManager.Spanish);
    }

    [Fact]
    public void TranslationLanguageManager_NormalizeLanguage_RejectsUnsupportedLanguages()
    {
        // Solo español e inglés permitidos por defecto
        Assert.Equal("es", TranslationLanguageManager.NormalizeLanguage("fr"));
        Assert.Equal("es", TranslationLanguageManager.NormalizeLanguage("de"));
        Assert.Equal("es", TranslationLanguageManager.NormalizeLanguage("ja"));
        Assert.Equal("es", TranslationLanguageManager.NormalizeLanguage(""));
        Assert.Equal("es", TranslationLanguageManager.NormalizeLanguage(null));

        Assert.Equal("en", TranslationLanguageManager.NormalizeLanguage("en"));
        Assert.Equal("en", TranslationLanguageManager.NormalizeLanguage("English"));
        Assert.Equal("en", TranslationLanguageManager.NormalizeLanguage("inglés"));
        Assert.Equal("es", TranslationLanguageManager.NormalizeLanguage("es"));
        Assert.Equal("es", TranslationLanguageManager.NormalizeLanguage("español"));
    }

    [Fact]
    public void TranslationLanguageManager_ToggleLanguage_SwitchesCorrectly()
    {
        TranslationLanguageManager.SetLanguage(TranslationLanguageManager.Spanish);
        string toggled = TranslationLanguageManager.ToggleLanguage();
        Assert.Equal("en", toggled);
        Assert.Equal("EN", TranslationLanguageManager.CurrentLanguageBadge);

        string toggledBack = TranslationLanguageManager.ToggleLanguage();
        Assert.Equal("es", toggledBack);
        Assert.Equal("ES", TranslationLanguageManager.CurrentLanguageBadge);
    }

    [Fact]
    public void ParseDictChromeResponse_MultipleBlocks_ParsesCorrectly()
    {
        string mockJson = "[[\"Hola mundo\",\"en\"],[\"Captura de pantalla\",\"en\"]]";
        var original = new[] { "Hello world", "Screen capture" };

        var results = GoogleTranslationService.ParseDictChromeResponse(mockJson, original, "es", "auto");

        Assert.NotNull(results);
        Assert.Equal(2, results.Count);
        Assert.Equal("Hola mundo", results[0].TranslatedText);
        Assert.Equal("en", results[0].DetectedSourceLanguage);
        Assert.Equal("Captura de pantalla", results[1].TranslatedText);
    }

    [Fact]
    public async Task TranslateBatchAsync_MultipleSentences_TranslatesAllInSingleCall()
    {
        var service = new GoogleTranslationService();
        var inputs = new[] { "Settings", "Account", "Full screen" };

        var results = await service.TranslateBatchAsync(inputs, targetLang: "es", sourceLang: "en");

        Assert.NotNull(results);
        Assert.Equal(3, results.Count);
        Assert.All(results, r => Assert.True(r.IsSuccess));
        Assert.Contains(results, r => r.TranslatedText.Contains("pantalla", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task TranslateAsync_RealEndpoint_TranslatesSpanishToEnglish()
    {
        // Arrange
        var service = new GoogleTranslationService();

        // Act
        var result = await service.TranslateAsync("Captura de pantalla con traducción instantánea", targetLang: "en", sourceLang: "es");

        // Assert
        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.TranslatedText));
        if (result.IsSuccess)
        {
            Assert.Contains("translation", result.TranslatedText, StringComparison.OrdinalIgnoreCase);
        }
    }
}
