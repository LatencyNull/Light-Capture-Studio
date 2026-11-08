using System.Windows.Media.Imaging;

namespace LightCapture.Core.Services;

public interface IQrScannerService
{
    string? ScanQrCode(BitmapSource bitmap);
}
