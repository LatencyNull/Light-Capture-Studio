using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace LightCapture.Core.Services;

public class ScreenCaptureService : ICaptureService
{
    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    private const int SM_XVIRTUALSCREEN = 76;
    private const int SM_YVIRTUALSCREEN = 77;
    private const int SM_CXVIRTUALSCREEN = 78;
    private const int SM_CYVIRTUALSCREEN = 79;

    public ScreenBounds GetVirtualScreenBounds()
    {
        int left = GetSystemMetrics(SM_XVIRTUALSCREEN);
        int top = GetSystemMetrics(SM_YVIRTUALSCREEN);
        int width = GetSystemMetrics(SM_CXVIRTUALSCREEN);
        int height = GetSystemMetrics(SM_CYVIRTUALSCREEN);

        if (width <= 0 || height <= 0)
        {
            left = 0;
            top = 0;
            width = (int)SystemParameters.PrimaryScreenWidth;
            height = (int)SystemParameters.PrimaryScreenHeight;
        }

        return new ScreenBounds(left, top, width, height);
    }

    public BitmapSource CaptureVirtualScreen()
    {
        var bounds = GetVirtualScreenBounds();

        using var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, new System.Drawing.Size(bounds.Width, bounds.Height), CopyPixelOperation.SourceCopy);
        }

        IntPtr hBitmap = bitmap.GetHbitmap();
        try
        {
            var bitmapSource = Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions()
            );

            bitmapSource.Freeze(); // Hace el BitmapSource thread-safe
            return bitmapSource;
        }
        finally
        {
            DeleteObject(hBitmap);
        }
    }

    public BitmapSource CropBitmap(BitmapSource source, int x, int y, int width, int height)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (width <= 0 || height <= 0) throw new ArgumentException("El ancho y alto deben ser positivos");

        // Clamp para evitar sobrepasar límites
        int safeX = Math.Max(0, Math.Min(x, source.PixelWidth - 1));
        int safeY = Math.Max(0, Math.Min(y, source.PixelHeight - 1));
        int safeW = Math.Min(width, source.PixelWidth - safeX);
        int safeH = Math.Min(height, source.PixelHeight - safeY);

        var cropped = new CroppedBitmap(source, new Int32Rect(safeX, safeY, safeW, safeH));
        cropped.Freeze();
        return cropped;
    }

    public static byte[] BitmapSourceToPngBytes(BitmapSource bitmap)
    {
        if (bitmap == null) return Array.Empty<byte>();
        using var ms = new System.IO.MemoryStream();
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        encoder.Save(ms);
        return ms.ToArray();
    }
}
