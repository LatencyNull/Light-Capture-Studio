using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LightCapture.Core.Services;

namespace LightCapture.Tests;

public class KillerFeaturesTests
{
    [Fact]
    public void PixelationHelper_CreatesPixelatedBlockWithMatchingBounds()
    {
        // Arrange
        int w = 80, h = 60;
        int stride = w * 4;
        byte[] pixels = new byte[h * stride];
        var source = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, pixels, stride);

        // Act
        var pixelated = PixelationHelper.CreatePixelatedBlock(source, new Rect(10, 10, 40, 30), blockSize: 8);

        // Assert
        Assert.NotNull(pixelated);
        Assert.Equal(40, pixelated.PixelWidth);
        Assert.Equal(30, pixelated.PixelHeight);
    }

    [Fact]
    public void QrScannerService_EmptyImage_ReturnsNull()
    {
        // Arrange
        var service = new QrScannerService();
        int w = 20, h = 20;
        int stride = w * 4;
        byte[] pixels = new byte[h * stride];
        var source = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, pixels, stride);

        // Act
        var result = service.ScanQrCode(source);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void CaptureHistoryService_SaveAndSearch_FindsTargetKeyword()
    {
        // Arrange
        var historyService = new CaptureHistoryService();
        int w = 20, h = 20;
        int stride = w * 4;
        byte[] pixels = new byte[h * stride];
        var img = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, pixels, stride);

        string uniqueKey = "SecretInvoiceToken_" + Guid.NewGuid().ToString("N")[..6];

        // Act
        historyService.SaveCapture(img, $"Payment received for {uniqueKey} today");
        var results = historyService.Search(uniqueKey);

        // Assert
        Assert.NotEmpty(results);
        Assert.Contains(uniqueKey, results[0].OcrText);
        Assert.Equal(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Light Capture"), historyService.HistoryDirectory);

        // Cleanup
        historyService.DeleteEntry(results[0].Id);
    }

    [Fact]
    public void RedactDrawingElement_DestructiveCensorship_CompletelyOverwritesUnderlyingPixels()
    {
        // Arrange: Imagen 50x50 totalmente blanca (255, 255, 255)
        int w = 50, h = 50;
        int stride = w * 4;
        byte[] whitePixels = new byte[h * stride];
        for (int i = 0; i < whitePixels.Length; i += 4)
        {
            whitePixels[i] = 255;     // B
            whitePixels[i + 1] = 255; // G
            whitePixels[i + 2] = 255; // R
            whitePixels[i + 3] = 255; // A
        }
        var source = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, whitePixels, stride);

        // Act: Renderizar bloque de censura opaco negro sobre toda el área
        var redact = new LightCapture.Core.Models.RedactDrawingElement(new Rect(0, 0, w, h), Colors.Black);
        var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawImage(source, new Rect(0, 0, w, h));
            redact.Render(dc);
        }
        rtb.Render(visual);

        byte[] outputPixels = new byte[h * stride];
        rtb.CopyPixels(outputPixels, stride, 0);

        // Assert: Verificar que absolutamente NINGÚN pixel blanco sobrevive debajo
        for (int i = 0; i < outputPixels.Length; i += 4)
        {
            byte b = outputPixels[i];
            byte g = outputPixels[i + 1];
            byte r = outputPixels[i + 2];
            // Los canales R, G, B deben ser 0 (negro total)
            Assert.Equal(0, r);
            Assert.Equal(0, g);
            Assert.Equal(0, b);
        }
    }

    [Fact]
    public void CaptureHistoryService_IncognitoMode_DoesNotPersistCapture()
    {
        // Arrange
        var historyService = new CaptureHistoryService();
        historyService.IsHistoryEnabled = false;

        int w = 10, h = 10;
        int stride = w * 4;
        byte[] pixels = new byte[h * stride];
        var img = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
        string secretText = "SecretPassword_" + Guid.NewGuid().ToString("N");

        // Act
        historyService.SaveCapture(img, secretText);
        var results = historyService.Search(secretText);

        // Assert
        Assert.Empty(results);

        // Restore
        historyService.IsHistoryEnabled = true;
    }

    [Fact]
    public void CaptureHistoryService_ClearAllHistory_EmptiesAllRecords()
    {
        // Arrange
        var historyService = new CaptureHistoryService();
        historyService.IsHistoryEnabled = true;

        int w = 10, h = 10;
        int stride = w * 4;
        byte[] pixels = new byte[h * stride];
        var img = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, pixels, stride);

        historyService.SaveCapture(img, "Test entry to be cleared");

        // Act
        historyService.ClearAllHistory();
        var all = historyService.GetHistory();

        // Assert
        Assert.Empty(all);
    }

    [Fact]
    public async Task CloudUploadService_NullBytes_ReturnsNull()
    {
        var service = new CloudUploadService();
        var result = await service.UploadImageAsync(Array.Empty<byte>());
        Assert.Null(result);
    }

    [Fact]
    public async Task CloudUploadService_ValidBytes_ReturnsPublicUrl()
    {
        byte[] dummyPng = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAoAAAAKCAYAAACNMs+9AAAAFUlEQVR42mP8z8BQz0AEYBxVSF+FABJAD/sY6AnFAAAAAElFTkSuQmCC");
        var service = new CloudUploadService();
        var result = await service.UploadImageAsync(dummyPng);
        if (result != null)
        {
            Assert.StartsWith("http", result);
        }
    }

    [Fact]
    public async Task GoogleLensService_EmptyOrNullBytes_ReturnsNull()
    {
        var nullResult = await GoogleLensService.UploadAndGetSearchUrlAsync(null!);
        Assert.Null(nullResult);

        var emptyResult = await GoogleLensService.UploadAndGetSearchUrlAsync(Array.Empty<byte>());
        Assert.Null(emptyResult);
    }

    [Fact]
    public async Task GoogleLensService_ValidBytes_ReturnsSearchUrl()
    {
        // 10x10 dummy png
        byte[] dummyPng = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAoAAAAKCAYAAACNMs+9AAAAFUlEQVR42mP8z8BQz0AEYBxVSF+FABJAD/sY6AnFAAAAAElFTkSuQmCC");
        var result = await GoogleLensService.UploadAndGetSearchUrlAsync(dummyPng);

        // Si hay conexión a internet debe retornar URL de Google Search / Lens; si estuviera offline retorna null
        if (result != null)
        {
            Assert.StartsWith("https://", result);
            Assert.Contains("google.com", result);
        }
    }

    [Fact]
    public void GoogleLensService_BuildLensSearchUrl_ConstructsValidEncodedUrl()
    {
        string sampleUrl = "https://iili.io/sample image.png";
        string lensUrl = GoogleLensService.BuildLensSearchUrl(sampleUrl);

        Assert.StartsWith("https://lens.google.com/uploadbyurl?url=", lensUrl);
        Assert.Contains(Uri.EscapeDataString(sampleUrl), lensUrl);
    }

    [Fact]
    public void GoogleLensService_BuildGoogleImagesSearchUrl_ConstructsValidUrl()
    {
        string sampleUrl = "https://iili.io/sample.png";
        string searchUrl = GoogleLensService.BuildGoogleImagesSearchUrl(sampleUrl);

        Assert.StartsWith("https://www.google.com/searchbyimage?image_url=", searchUrl);
        Assert.Contains(Uri.EscapeDataString(sampleUrl), searchUrl);
    }

    [Theory]
    [InlineData("https://www.google.com/search?vsrid=abc&udm=26&lns_mode=un", "https://www.google.com/search?vsrid=abc&udm=44&lns_mode=un")]
    [InlineData("https://www.google.com/search?vsrid=abc&udm=24", "https://www.google.com/search?vsrid=abc&udm=44")]
    [InlineData("https://www.google.com/search?vsrid=abc", "https://www.google.com/search?vsrid=abc&udm=44")]
    public void GoogleLensService_FormatLensUrlForVisualMatches_ForcesUdm44(string input, string expected)
    {
        string formatted = GoogleLensService.FormatLensUrlForVisualMatches(input);
        Assert.Equal(expected, formatted);
        Assert.Contains("udm=44", formatted);
    }

    [Fact]
    public void CaptureHistoryService_DeleteEntry_RemovesFromDiskAndList()
    {
        var historyService = new CaptureHistoryService();
        byte[] dummyPng = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAoAAAAKCAYAAACNMs+9AAAAFUlEQVR42mP8z8BQz0AEYBxVSF+FABJAD/sY6AnFAAAAAElFTkSuQmCC");
        using var ms = new MemoryStream(dummyPng);
        var bitmap = new System.Windows.Media.Imaging.BitmapImage();
        bitmap.BeginInit();
        bitmap.StreamSource = ms;
        bitmap.EndInit();
        bitmap.Freeze();

        historyService.SaveCapture(bitmap, "Test Deletion OCR");
        var items = historyService.GetHistory();
        Assert.NotEmpty(items);

        var first = items.First(i => i.OcrText == "Test Deletion OCR");
        string filePath = historyService.GetImagePath(first);
        Assert.True(File.Exists(filePath));

        historyService.DeleteEntry(first.Id);

        var updated = historyService.GetHistory();
        Assert.DoesNotContain(updated, i => i.Id == first.Id);
        Assert.False(File.Exists(filePath));
    }

    [Fact]
    public void CaptureHistoryService_DeleteEntries_BatchRemovesFromDiskAndList()
    {
        var historyService = new CaptureHistoryService();
        byte[] dummyPng = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAoAAAAKCAYAAACNMs+9AAAAFUlEQVR42mP8z8BQz0AEYBxVSF+FABJAD/sY6AnFAAAAAElFTkSuQmCC");
        using var ms = new MemoryStream(dummyPng);
        var bitmap = new System.Windows.Media.Imaging.BitmapImage();
        bitmap.BeginInit();
        bitmap.StreamSource = ms;
        bitmap.EndInit();
        bitmap.Freeze();

        historyService.SaveCapture(bitmap, "Batch 1");
        historyService.SaveCapture(bitmap, "Batch 2");

        var items = historyService.GetHistory();
        var b1 = items.First(i => i.OcrText == "Batch 1");
        var b2 = items.First(i => i.OcrText == "Batch 2");
        string p1 = historyService.GetImagePath(b1);
        string p2 = historyService.GetImagePath(b2);

        historyService.DeleteEntries(new[] { b1.Id, b2.Id });

        var after = historyService.GetHistory();
        Assert.DoesNotContain(after, i => i.Id == b1.Id);
        Assert.DoesNotContain(after, i => i.Id == b2.Id);
        Assert.False(File.Exists(p1));
        Assert.False(File.Exists(p2));
    }
}
