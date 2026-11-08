using System.Windows.Media.Imaging;
using LightCapture.Core.Models;

namespace LightCapture.Core.Services;

public interface IOcrService
{
    Task<IReadOnlyList<OcrTextBlock>> RecognizeTextAsync(BitmapSource bitmap, string? languageCode = null);
}
