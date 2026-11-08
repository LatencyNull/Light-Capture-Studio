namespace LightCapture.Core.Models;

public record TranslationResult(
    string OriginalText,
    string TranslatedText,
    string DetectedSourceLanguage = "auto",
    string TargetLanguage = "es",
    bool IsSuccess = true,
    string? ErrorMessage = null
);
