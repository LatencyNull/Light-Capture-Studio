using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LightCapture.Core.Services;

public static class StudioMockupHelper
{
    public static BitmapSource CreateStudioMockup(BitmapSource source, int padding = 36)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));

        int origW = source.PixelWidth;
        int origH = source.PixelHeight;

        int totalW = origW + (padding * 2);
        int totalH = origH + (padding * 2);

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            // 1. Fondo degradado moderno (Púrpura noche a Índigo profundo)
            var gradient = new LinearGradientBrush(
                System.Windows.Media.Color.FromRgb(49, 46, 129),   // Indigo 900
                System.Windows.Media.Color.FromRgb(88, 28, 135),   // Purple 900
                new System.Windows.Point(0, 0),
                new System.Windows.Point(1, 1)
            );
            gradient.Freeze();
            dc.DrawRectangle(gradient, null, new Rect(0, 0, totalW, totalH));

            // 2. Sombra difuminada suave detrás de la captura
            var shadowBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(90, 0, 0, 0));
            shadowBrush.Freeze();
            dc.DrawRoundedRectangle(shadowBrush, null, new Rect(padding + 4, padding + 8, origW, origH), 10, 10);

            // 3. Imagen con esquinas redondeadas elegantes (Clip)
            var clipRect = new RectangleGeometry(new Rect(padding, padding, origW, origH), 8, 8);
            clipRect.Freeze();
            dc.PushClip(clipRect);

            dc.DrawImage(source, new Rect(padding, padding, origW, origH));
            dc.Pop();

            // 4. Borde sutil transparente alrededor de la captura
            var borderPen = new System.Windows.Media.Pen(new SolidColorBrush(System.Windows.Media.Color.FromArgb(40, 255, 255, 255)), 1);
            borderPen.Freeze();
            dc.DrawRoundedRectangle(null, borderPen, new Rect(padding, padding, origW, origH), 8, 8);
        }

        var rtb = new RenderTargetBitmap(totalW, totalH, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(visual);
        rtb.Freeze();
        return rtb;
    }
}
