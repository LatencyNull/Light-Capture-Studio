using LightCapture.Core.Models;

namespace LightCapture.Core.Services;

public interface ITranslationService
{
    Task<TranslationResult> TranslateAsync(string text, string targetLang = "es", string sourceLang = "auto");
}
