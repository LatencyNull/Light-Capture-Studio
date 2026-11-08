using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LightCapture.Core.Services;

public static class PixelationHelper
{
    public static BitmapSource CreatePixelatedBlock(BitmapSource source, Rect bounds, int blockSize = 10)
    {
        if (source == null || bounds.Width <= 0 || bounds.Height <= 0)
        {
            throw new ArgumentException("Parámetros de recorte inválidos");
        }

        int x = Math.Max(0, (int)bounds.X);
        int y = Math.Max(0, (int)bounds.Y);
        int w = Math.Min((int)bounds.Width, source.PixelWidth - x);
        int h = Math.Min((int)bounds.Height, source.PixelHeight - y);

        if (w <= 0 || h <= 0)
        {
            return source;
        }

        // 1. Recortar área objetivo
        var cropped = new CroppedBitmap(source, new Int32Rect(x, y, w, h));

        // 2. Reducir resolución para perder detalle (mosaico)
        int downW = Math.Max(1, w / blockSize);
        int downH = Math.Max(1, h / blockSize);

        var downscaled = new TransformedBitmap(cropped, new ScaleTransform((double)downW / w, (double)downH / h));

        // 3. Reescalar con interpolación Vecino Más Cercano (NearestNeighbor)
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.NearestNeighbor);
            dc.DrawImage(downscaled, new Rect(0, 0, w, h));
        }

        var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(visual);
        rtb.Freeze();
        return rtb;
    }
}
