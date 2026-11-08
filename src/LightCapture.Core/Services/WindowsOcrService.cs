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
