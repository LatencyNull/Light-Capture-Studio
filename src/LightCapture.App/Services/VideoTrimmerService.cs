using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using Windows.Media.Editing;
using Windows.Storage;

namespace LightCapture.App.Services;

public static class VideoTrimmerService
{
    /// <summary>
    /// Recorta un video MP4 o GIF según el rango de inicio y fin especificado.
    /// Retorna la ruta al nuevo archivo recortado en la carpeta temporal.
    /// </summary>
    public static async Task<string> TrimAsync(string sourcePath, TimeSpan startTime, TimeSpan endTime)
    {
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("El archivo fuente no existe.", sourcePath);
        }

        if (startTime >= endTime)
        {
            throw new ArgumentException("El tiempo de inicio debe ser menor al tiempo de fin.");
        }

        bool isGif = sourcePath.EndsWith(".gif", StringComparison.OrdinalIgnoreCase);
        if (isGif)
        {
            return await TrimGifAsync(sourcePath, startTime, endTime);
        }
        else
        {
            return await TrimMp4Async(sourcePath, startTime, endTime);
        }
    }

    private static async Task<string> TrimMp4Async(string sourcePath, TimeSpan startTime, TimeSpan endTime)
    {
        string fullSource = Path.GetFullPath(sourcePath);
        var sourceFile = await StorageFile.GetFileFromPathAsync(fullSource);
        var clip = await MediaClip.CreateFromFileAsync(sourceFile);

        clip.TrimTimeFromStart = startTime;
        if (endTime < clip.OriginalDuration)
        {
            clip.TrimTimeFromEnd = clip.OriginalDuration - endTime;
        }

        var composition = new MediaComposition();
        composition.Clips.Add(clip);

        string tempDir = Path.Combine(Path.GetTempPath(), "LightCapture_Recordings");
        if (!Directory.Exists(tempDir)) Directory.CreateDirectory(tempDir);

        string destFileName = $"Trim_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.mp4";
        string destPath = Path.Combine(tempDir, destFileName);

        var destFolder = await StorageFolder.GetFolderFromPathAsync(tempDir);
        var destFile = await destFolder.CreateFileAsync(destFileName, CreationCollisionOption.ReplaceExisting);

        await composition.RenderToFileAsync(destFile);

        return destPath;
    }

    private static Task<string> TrimGifAsync(string sourcePath, TimeSpan startTime, TimeSpan endTime)
    {
        return Task.Run(() =>
        {
            using var img = System.Drawing.Image.FromFile(sourcePath);
            var fd = new FrameDimension(img.FrameDimensionsList[0]);
            int totalFrames = img.GetFrameCount(fd);

            if (totalFrames <= 1) return sourcePath;

            int totalDurationMs = 0;
            byte[]? delays = null;
            try
            {
                var propItem = img.GetPropertyItem(0x5100);
                delays = propItem?.Value;
            }
            catch { }

            int defaultDelayMs = 70;
            int[] frameDelays = new int[totalFrames];
            for (int i = 0; i < totalFrames; i++)
            {
                int delayUnits = 7;
                if (delays != null && delays.Length >= (i + 1) * 4)
                {
                    delayUnits = BitConverter.ToInt32(delays, i * 4);
                }
                frameDelays[i] = delayUnits > 0 ? delayUnits * 10 : defaultDelayMs;
                totalDurationMs += frameDelays[i];
            }

            int startMs = (int)startTime.TotalMilliseconds;
            int endMs = (int)endTime.TotalMilliseconds;
            if (endMs <= 0 || endMs > totalDurationMs) endMs = totalDurationMs;

            int currentAccMs = 0;
            var extractedBitmaps = new List<Bitmap>();

            for (int i = 0; i < totalFrames; i++)
            {
                int fStart = currentAccMs;
                int fEnd = currentAccMs + frameDelays[i];
                currentAccMs = fEnd;

                if (fEnd >= startMs && fStart <= endMs)
                {
                    img.SelectActiveFrame(fd, i);
                    extractedBitmaps.Add(new Bitmap(img));
                }
            }

            if (extractedBitmaps.Count == 0) return sourcePath;

            string tempDir = Path.Combine(Path.GetTempPath(), "LightCapture_Recordings");
            if (!Directory.Exists(tempDir)) Directory.CreateDirectory(tempDir);
            string destPath = Path.Combine(tempDir, $"Trim_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.gif");

            SaveAsAnimatedGif(extractedBitmaps, destPath, 7);

            foreach (var b in extractedBitmaps)
            {
                b.Dispose();
            }

            return destPath;
        });
    }

    private static void SaveAsAnimatedGif(List<Bitmap> frames, string outputPath, int delayUnits = 7)
    {
        if (frames == null || frames.Count == 0) return;

        using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
        using var bw = new BinaryWriter(fs);

        byte[] gifAnimationBlock = new byte[]
        {
            0x21, 0xFF, 0x0B,
            (byte)'N', (byte)'E', (byte)'T', (byte)'S', (byte)'C', (byte)'A', (byte)'P', (byte)'E',
            (byte)'2', (byte)'.', (byte)'0',
            0x03, 0x01, 0x00, 0x00, 0x00
        };

        for (int i = 0; i < frames.Count; i++)
        {
            using var ms = new MemoryStream();
            frames[i].Save(ms, System.Drawing.Imaging.ImageFormat.Gif);
            byte[] bytes = ms.ToArray();

            if (i == 0)
            {
                // Cabecera GIF89a + Logical Screen Descriptor + Color Table
                int headerLen = 13;
                bool hasGct = (bytes[10] & 0x80) != 0;
                if (hasGct)
                {
                    int gctSize = 1 << ((bytes[10] & 0x07) + 1);
                    headerLen += 3 * gctSize;
                }

                bw.Write(bytes, 0, headerLen);
                bw.Write(gifAnimationBlock);

                // Inyectar delay y datos del primer frame
                InjectFrame(bytes, headerLen, bw, delayUnits);
            }
            else
            {
                int headerLen = 13;
                bool hasGct = (bytes[10] & 0x80) != 0;
                if (hasGct)
                {
                    int gctSize = 1 << ((bytes[10] & 0x07) + 1);
                    headerLen += 3 * gctSize;
                }

                InjectFrame(bytes, headerLen, bw, delayUnits);
            }
        }

        // Trailer de fin de GIF
        bw.Write((byte)0x3B);
    }

    private static void InjectFrame(byte[] bytes, int offset, BinaryWriter bw, int delayUnits)
    {
        while (offset < bytes.Length && bytes[offset] != 0x3B)
        {
            if (bytes[offset] == 0x21 && offset + 1 < bytes.Length && bytes[offset + 1] == 0xF9)
            {
                // Graphic Control Extension preexistente: reemplazar delay
                int blockLen = bytes[offset + 2];
                bytes[offset + 4] = (byte)(delayUnits & 0xFF);
                bytes[offset + 5] = (byte)((delayUnits >> 8) & 0xFF);
                int totalExtLen = 3 + blockLen + 1;
                bw.Write(bytes, offset, totalExtLen);
                offset += totalExtLen;
            }
            else if (bytes[offset] == 0x2C)
            {
                // Inyectar Graphic Control Extension antes del Image Descriptor si no existía
                byte[] gce = new byte[]
                {
                    0x21, 0xF9, 0x04,
                    0x08,
                    (byte)(delayUnits & 0xFF),
                    (byte)((delayUnits >> 8) & 0xFF),
                    0x00, 0x00
                };
                bw.Write(gce);

                // Escribir resto del frame hasta el siguiente bloque o fin
                int start = offset;
                offset++;
                bool hasLct = (bytes[offset + 8] & 0x80) != 0;
                offset += 9;
                if (hasLct)
                {
                    int lctSize = 1 << ((bytes[start + 9] & 0x07) + 1);
                    offset += 3 * lctSize;
                }
                offset++; // LZW minimum code size
                while (offset < bytes.Length && bytes[offset] != 0x00)
                {
                    offset += bytes[offset] + 1;
                }
                offset++; // Terminating 0x00

                bw.Write(bytes, start, offset - start);
                break;
            }
            else
            {
                offset++;
            }
        }
    }
}
