using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Media.Editing;
using Windows.Storage;

namespace LightCapture.App.Services;

public record TimedOverlay(string ImagePath, TimeSpan StartTime, TimeSpan EndTime);

public static class VideoTrimmerService
{
    /// <summary>
    /// Recorta un video MP4 o GIF según el rango de inicio y fin especificado,
    /// y opcionalmente estampa una o varias capas de anotaciones con tiempos específicos.
    /// Retorna la ruta al nuevo archivo procesado en la carpeta temporal.
    /// </summary>
    public static async Task<string> TrimAsync(
        string sourcePath, 
        TimeSpan startTime, 
        TimeSpan endTime, 
        string? overlayPngPath = null,
        IReadOnlyList<TimedOverlay>? timedOverlays = null)
    {
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("Source file does not exist.", sourcePath);
        }

        if (startTime >= endTime)
        {
            throw new ArgumentException("Start time must be less than end time.");
        }

        bool isGif = sourcePath.EndsWith(".gif", StringComparison.OrdinalIgnoreCase);
        if (isGif)
        {
            return await TrimGifAsync(sourcePath, startTime, endTime, overlayPngPath, timedOverlays);
        }
        else
        {
            return await TrimMp4Async(sourcePath, startTime, endTime, overlayPngPath, timedOverlays);
        }
    }

    private static async Task<string> TrimMp4Async(
        string sourcePath, 
        TimeSpan startTime, 
        TimeSpan endTime, 
        string? overlayPngPath = null,
        IReadOnlyList<TimedOverlay>? timedOverlays = null)
    {
        bool hasOverlays = (timedOverlays != null && timedOverlays.Count > 0) ||
                           (!string.IsNullOrEmpty(overlayPngPath) && File.Exists(overlayPngPath));

        // 1. Si hay overlay y FFmpeg está instalado, usar aceleración FFmpeg (sub-segundo)
        if (hasOverlays && IsFfmpegAvailable())
        {
            string? ffmpegResult = await TryFfmpegOverlayAsync(sourcePath, startTime, endTime, overlayPngPath, timedOverlays);
            if (!string.IsNullOrEmpty(ffmpegResult) && File.Exists(ffmpegResult))
            {
                return ffmpegResult;
            }
        }

        // 2. Si no hay FFmpeg o falló, procesar con Windows.Media.Editing nativo
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

        if (!string.IsNullOrEmpty(overlayPngPath) && File.Exists(overlayPngPath))
        {
            try
            {
                var overlayFile = await StorageFile.GetFileFromPathAsync(Path.GetFullPath(overlayPngPath));
                var overlayClip = await MediaClip.CreateFromImageFileAsync(overlayFile, clip.TrimmedDuration);
                var overlay = new MediaOverlay(overlayClip);
                overlay.Opacity = 1.0;
                var overlayLayer = new MediaOverlayLayer();
                overlayLayer.Overlays.Add(overlay);
                composition.OverlayLayers.Add(overlayLayer);
            }
            catch { }
        }
        else if (timedOverlays != null && timedOverlays.Count > 0)
        {
            try
            {
                var overlayLayer = new MediaOverlayLayer();
                foreach (var to in timedOverlays)
                {
                    if (File.Exists(to.ImagePath) && to.EndTime > startTime && to.StartTime < endTime)
                    {
                        var overlayFile = await StorageFile.GetFileFromPathAsync(Path.GetFullPath(to.ImagePath));
                        var s = to.StartTime > startTime ? to.StartTime - startTime : TimeSpan.Zero;
                        var e = to.EndTime < endTime ? to.EndTime - startTime : clip.TrimmedDuration;
                        var dur = e - s;
                        if (dur > TimeSpan.Zero)
                        {
                            var overlayClip = await MediaClip.CreateFromImageFileAsync(overlayFile, dur);
                            var overlay = new MediaOverlay(overlayClip) { Delay = s, Opacity = 1.0 };
                            overlayLayer.Overlays.Add(overlay);
                        }
                    }
                }
                if (overlayLayer.Overlays.Count > 0)
                {
                    composition.OverlayLayers.Add(overlayLayer);
                }
            }
            catch { }
        }

        string tempDir = Path.Combine(Path.GetTempPath(), "LightCapture_Recordings");
        if (!Directory.Exists(tempDir)) Directory.CreateDirectory(tempDir);

        string destFileName = $"Trim_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.mp4";
        string destPath = Path.Combine(tempDir, destFileName);

        var destFolder = await StorageFolder.GetFolderFromPathAsync(tempDir);
        var destFile = await destFolder.CreateFileAsync(destFileName, CreationCollisionOption.ReplaceExisting);

        await composition.RenderToFileAsync(destFile);

        return destPath;
    }

    private static bool IsFfmpegAvailable()
    {
        try
        {
            using var p = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = "-version",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };
            p.Start();
            p.WaitForExit(1000);
            return p.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<string?> TryFfmpegOverlayAsync(
        string sourcePath, 
        TimeSpan startTime, 
        TimeSpan endTime, 
        string? overlayPngPath,
        IReadOnlyList<TimedOverlay>? timedOverlays = null)
    {
        return await Task.Run(() =>
        {
            try
            {
                string tempDir = Path.Combine(Path.GetTempPath(), "LightCapture_Recordings");
                if (!Directory.Exists(tempDir)) Directory.CreateDirectory(tempDir);

                string destPath = Path.Combine(tempDir, $"Annotated_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.mp4");
                double startSec = Math.Max(0, startTime.TotalSeconds);
                double durationSec = Math.Max(0.05, (endTime - startTime).TotalSeconds);

                var inputs = new StringBuilder();
                inputs.Append($"-y -ss {startSec.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)} -t {durationSec.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)} -i \"{Path.GetFullPath(sourcePath)}\"");

                string filterComplex;

                if (timedOverlays != null && timedOverlays.Count > 0)
                {
                    var valid = timedOverlays.Where(t => File.Exists(t.ImagePath) && t.EndTime > startTime && t.StartTime < endTime).ToList();
                    if (valid.Count == 0)
                    {
                        return null;
                    }

                    for (int i = 0; i < valid.Count; i++)
                    {
                        inputs.Append($" -i \"{Path.GetFullPath(valid[i].ImagePath)}\"");
                    }

                    var filters = new StringBuilder();
                    string lastLabel = "0:v";
                    for (int i = 0; i < valid.Count; i++)
                    {
                        var to = valid[i];
                        double s = Math.Max(0, (to.StartTime - startTime).TotalSeconds);
                        double e = Math.Min(durationSec, (to.EndTime - startTime).TotalSeconds);
                        string outLabel = (i == valid.Count - 1) ? "v" : $"v{i + 1}";
                        string sStr = s.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
                        string eStr = e.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);

                        filters.Append($"[{lastLabel}][{i + 1}:v]overlay=0:0:enable='between(t,{sStr},{eStr})'[{outLabel}];");
                        lastLabel = outLabel;
                    }

                    filterComplex = filters.ToString().TrimEnd(';');
                }
                else if (!string.IsNullOrEmpty(overlayPngPath) && File.Exists(overlayPngPath))
                {
                    inputs.Append($" -i \"{Path.GetFullPath(overlayPngPath)}\"");
                    filterComplex = "[0:v][1:v]overlay=0:0[v]";
                }
                else
                {
                    return null;
                }

                var psi = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = $"{inputs} -filter_complex \"{filterComplex}\" -map \"[v]\" -map 0:a? -c:v libx264 -preset veryfast -crf 20 -c:a aac -b:a 128k \"{destPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var proc = Process.Start(psi);
                if (proc == null) return null;
                proc.WaitForExit(30000);
                return proc.ExitCode == 0 && File.Exists(destPath) ? destPath : null;
            }
            catch
            {
                return null;
            }
        });
    }

    private static Task<string> TrimGifAsync(
        string sourcePath, 
        TimeSpan startTime, 
        TimeSpan endTime, 
        string? overlayPngPath = null,
        IReadOnlyList<TimedOverlay>? timedOverlays = null)
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

            using var overlayImg = (!string.IsNullOrEmpty(overlayPngPath) && File.Exists(overlayPngPath))
                ? System.Drawing.Image.FromFile(overlayPngPath)
                : null;

            var loadedTimed = new List<(TimedOverlay Meta, System.Drawing.Image Img)>();
            if (timedOverlays != null)
            {
                foreach (var to in timedOverlays)
                {
                    if (File.Exists(to.ImagePath))
                    {
                        try { loadedTimed.Add((to, System.Drawing.Image.FromFile(to.ImagePath))); } catch { }
                    }
                }
            }

            try
            {
                for (int i = 0; i < totalFrames; i++)
                {
                    int fStart = currentAccMs;
                    int fEnd = currentAccMs + frameDelays[i];
                    currentAccMs = fEnd;

                    if (fEnd >= startMs && fStart <= endMs)
                    {
                        img.SelectActiveFrame(fd, i);
                        var frameBmp = new Bitmap(img);
                        var frameTime = TimeSpan.FromMilliseconds(fStart);

                        using (var g = Graphics.FromImage(frameBmp))
                        {
                            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                            if (overlayImg != null)
                            {
                                g.DrawImage(overlayImg, 0, 0, frameBmp.Width, frameBmp.Height);
                            }
                            foreach (var (meta, overlayImage) in loadedTimed)
                            {
                                if (frameTime >= meta.StartTime && frameTime <= meta.EndTime)
                                {
                                    g.DrawImage(overlayImage, 0, 0, frameBmp.Width, frameBmp.Height);
                                }
                            }
                        }
                        extractedBitmaps.Add(frameBmp);
                    }
                }
            }
            finally
            {
                foreach (var (_, overlayImage) in loadedTimed)
                {
                    overlayImage.Dispose();
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
