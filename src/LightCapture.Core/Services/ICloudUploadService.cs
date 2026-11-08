namespace LightCapture.Core.Services;

public interface ICloudUploadService
{
    Task<string?> UploadImageAsync(byte[] imageBytes, string fileName = "capture.png");
}
