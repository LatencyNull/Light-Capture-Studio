using LightCapture.Core.Models;
using LightCapture.Core.Services;

namespace LightCapture.Tests;

public class OcrBoundingBoxTests
{
    [Fact]
    public void MergeWordBoxes_CombinesWordsIntoUnifiedLineBox()
    {
        // Arrange: 3 palabras adyacentes en una misma línea
        var word1 = new OcrWordBlock("Hello", 10, 20, 50, 25);
        var word2 = new OcrWordBlock("beautiful", 65, 21, 70, 24);
        var word3 = new OcrWordBlock("world", 140, 19, 45, 26);

        // Act
        var lineBlock = OcrTextBlock.FromWords(new[] { word1, word2, word3 });

        // Assert
        Assert.Equal("Hello beautiful world", lineBlock.Text);
        Assert.Equal(10, lineBlock.X);
        Assert.Equal(19, lineBlock.Y); // El mínimo Y
        Assert.Equal(175, lineBlock.Width); // De 10 a 140+45 = 185 -> ancho 175
        Assert.Equal(26, lineBlock.Height);
    }

    [Fact]
    public void EmptyWords_ReturnsZeroBox()
    {
        // Act
        var emptyBlock = OcrTextBlock.FromWords(Array.Empty<OcrWordBlock>());

        // Assert
        Assert.Equal(string.Empty, emptyBlock.Text);
        Assert.Equal(0, emptyBlock.Width);
        Assert.Equal(0, emptyBlock.Height);
    }

    [Fact]
    public void WindowsOcrService_AvailableLanguages_ContainsSupportedLanguage()
    {
        // Act
        var isAvailable = WindowsOcrService.IsOcrAvailable();

        // Assert: En cualquier Windows 10/11 moderno debe estar disponible al menos un reconocedor
        Assert.True(isAvailable);
    }
}
