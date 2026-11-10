using System.IO;
using Windows.Globalization;
using Windows.Media.Ocr;
using LightCapture.Core.Models;

namespace LightCapture.Core.Services;

public class WindowsOcrService : IOcrService
{
    public static bool IsOcrAvailable()
    {
        return OcrEngine.AvailableRecognizerLanguages.Count > 0;
    }

    public static IReadOnlyList<string> GetAvailableLanguages()
    {
        return OcrEngine.AvailableRecognizerLanguages.Select(l => l.LanguageTag).ToList();
    }

    public async Task<IReadOnlyList<OcrTextBlock>> RecognizeTextAsync(System.Windows.Media.Imaging.BitmapSource bitmap, string? languageCode = null)
    {
        if (bitmap == null) return Array.Empty<OcrTextBlock>();

        // Obtener el motor de OCR para el idioma deseado o el predeterminado del sistema
        OcrEngine? engine = null;
        if (!string.IsNullOrWhiteSpace(languageCode))
        {
            var targetLang = new Language(languageCode);
            if (OcrEngine.IsLanguageSupported(targetLang))
            {
                engine = OcrEngine.TryCreateFromLanguage(targetLang);
            }
        }

        engine ??= OcrEngine.TryCreateFromUserProfileLanguages();

        if (engine == null && OcrEngine.AvailableRecognizerLanguages.Count > 0)
        {
            engine = OcrEngine.TryCreateFromLanguage(OcrEngine.AvailableRecognizerLanguages[0]);
        }

        if (engine == null)
        {
            return Array.Empty<OcrTextBlock>();
        }

        // Convertir BitmapSource a SoftwareBitmap compatible con WinRT
        using var memoryStream = new MemoryStream();
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        encoder.Save(memoryStream);
        memoryStream.Position = 0;

        using var randomAccessStream = memoryStream.AsRandomAccessStream();
        var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(randomAccessStream);
        using var softwareBitmap = await decoder.GetSoftwareBitmapAsync();

        // Ejecutar OCR nativo
        var ocrResult = await engine.RecognizeAsync(softwareBitmap);

        // Si no se detectó texto con el idioma inicial, probar los otros idiomas instalados (ej. en-US o es-ES)
        if (ocrResult.Lines.Count == 0 && OcrEngine.AvailableRecognizerLanguages.Count > 1)
        {
            foreach (var altLang in OcrEngine.AvailableRecognizerLanguages)
            {
                if (altLang.LanguageTag.Equals(engine.RecognizerLanguage.LanguageTag, StringComparison.OrdinalIgnoreCase))
                    continue;

                var altEngine = OcrEngine.TryCreateFromLanguage(altLang);
                if (altEngine != null)
                {
                    var altResult = await altEngine.RecognizeAsync(softwareBitmap);
                    if (altResult.Lines.Count > 0)
                    {
                        ocrResult = altResult;
                        break;
                    }
                }
            }
        }

        var blocks = new List<OcrTextBlock>();
        foreach (var line in ocrResult.Lines)
        {
            var words = new List<OcrWordBlock>();
            foreach (var word in line.Words)
            {
                words.Add(new OcrWordBlock(
                    Text: word.Text,
                    X: word.BoundingRect.X,
                    Y: word.BoundingRect.Y,
                    Width: word.BoundingRect.Width,
                    Height: word.BoundingRect.Height
                ));
            }

            if (words.Count > 0)
            {
                blocks.Add(OcrTextBlock.FromWords(words));
            }
        }

        return blocks;
    }
}
