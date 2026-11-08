using System.Windows.Media;
using System.Windows.Media.Imaging;
using LightCapture.Core.Services;

namespace LightCapture.Tests;

public class ScreenCaptureTests
{
    [Fact]
    public void GetVirtualScreenBounds_ReturnsValidPositiveDimensions()
    {
        // Arrange
        var service = new ScreenCaptureService();

        // Act
        var bounds = service.GetVirtualScreenBounds();

        // Assert
        Assert.True(bounds.Width > 0);
        Assert.True(bounds.Height > 0);
    }

    [Fact]
    public void CropBitmap_CropsCorrectDimensions()
    {
        // Arrange: Crear un bitmap de 100x100
        var service = new ScreenCaptureService();
        int width = 100;
        int height = 100;
        int stride = width * 4;
        byte[] pixels = new byte[height * stride];
        var source = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, stride);

        // Act: Recortar 40x30 desde (10, 10)
        var cropped = service.CropBitmap(source, 10, 10, 40, 30);

        // Assert
        Assert.Equal(40, cropped.PixelWidth);
        Assert.Equal(30, cropped.PixelHeight);
    }
}
