using System.Windows.Media;
using System.Windows.Media.Imaging;
using ZXing;
using ZXing.Common;

namespace LightCapture.Core.Services;

public class QrScannerService : IQrScannerService
{
    public string? ScanQrCode(BitmapSource bitmap)
    {
        if (bitmap == null || bitmap.PixelWidth < 10 || bitmap.PixelHeight < 10)
        {
            return null;
        }

        try
        {
            // Convertir a formato compatible Bgra32
            var formatted = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
            int width = formatted.PixelWidth;
            int height = formatted.PixelHeight;
            int stride = width * 4;
            byte[] pixelData = new byte[height * stride];
            formatted.CopyPixels(pixelData, stride, 0);

            // Crear LuminanceSource para ZXing
            var luminanceSource = new RGBLuminanceSource(pixelData, width, height, RGBLuminanceSource.BitmapFormat.BGRA32);
            var binarizer = new HybridBinarizer(luminanceSource);
            var binaryBitmap = new BinaryBitmap(binarizer);

            var hints = new Dictionary<DecodeHintType, object>
            {
                { DecodeHintType.POSSIBLE_FORMATS, new List<BarcodeFormat> { BarcodeFormat.QR_CODE, BarcodeFormat.DATA_MATRIX, BarcodeFormat.CODE_128 } },
                { DecodeHintType.TRY_HARDER, true }
            };

            var reader = new MultiFormatReader();
            var result = reader.decode(binaryBitmap, hints);

            return result?.Text;
        }
        catch
        {
            return null;
        }
    }
}
