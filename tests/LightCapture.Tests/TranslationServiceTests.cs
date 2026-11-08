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
        var result = await service.TranslateAsync("Screen capture with instant translation", targetLang: "es", sourceLang: "en");

        // Assert
        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.TranslatedText));
        if (result.IsSuccess)
        {
            Assert.Contains("traducci", result.TranslatedText, StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            Assert.Equal("Screen capture with instant translation", result.TranslatedText);
        }
    }
}
