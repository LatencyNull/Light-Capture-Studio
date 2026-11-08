using System.Windows.Media.Imaging;

namespace LightCapture.Core.Services;

public record ScreenBounds(int Left, int Top, int Width, int Height);

public interface ICaptureService
{
    ScreenBounds GetVirtualScreenBounds();
    BitmapSource CaptureVirtualScreen();
    BitmapSource CropBitmap(BitmapSource source, int x, int y, int width, int height);
}
